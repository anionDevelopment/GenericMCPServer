"""Returns the release-notes which one version of one nuget-package publishes.

This script is one of the tools the MCP-server offers. It is called by the BusinessLogicService and answers with a
json-document on stdout. An error is written to stderr and results in an exit-code which is not 0, which the server
turns into a failed tool-call.

Nuget is one of the two platforms of this example for which this tool exists: it publishes the release-notes of a
version as part of the metadata of that version. Maven-central and the npm-registry publish no such field, so a tool
which claimed to return their changelog would have to guess where it stands - which is why no such tool is declared for
them.

The values of the arguments come from the client of the MCP-server and are therefore untrusted. Each of them is checked
against the format it must have before it is used, which also keeps it from becoming a path-segment of its own inside
the url which is requested.
"""
import argparse
import gzip
import json
import re
import sys
import urllib.error
import urllib.parse
import urllib.request

# The registration-api answers with the metadata of exactly one version. The gzipped semver2-variant is used because it
# is the only one which contains every package, including the ones whose versions follow semver2.
REGISTRATION_URL = "https://api.nuget.org/v3/registration5-gz-semver2"
TIMEOUT_IN_SECONDS = 30

# The id of a nuget-package: letters, digits, dots, hyphens and underscores, not starting with a dot. Anchored at both
# ends, so a value which contains a slash or a path-segment does not match.
PACKAGE_ID_REGEX = re.compile(r"^[A-Za-z0-9_][A-Za-z0-9_.\-]*$")

# A version of a nuget-package, for example "13.0.3" or "13.0.4-beta1".
VERSION_REGEX = re.compile(r"^[0-9]+(\.[0-9]+)*([-+][A-Za-z0-9.\-]+)?$")


def package_id(value: str) -> str:
    """Returns the given value if it is a nuget-package-id and raises otherwise."""
    if PACKAGE_ID_REGEX.match(value) is None:
        raise argparse.ArgumentTypeError(f"\"{value}\" is not a nuget-package-id. Expected format: letters, digits, dots, hyphens and underscores, for example \"Newtonsoft.Json\".")
    return value


def version(value: str) -> str:
    """Returns the given value if it is a version and raises otherwise."""
    if VERSION_REGEX.match(value) is None:
        raise argparse.ArgumentTypeError(f"\"{value}\" is not a version. Expected format: numbers which are separated by dots and an optional suffix, for example \"13.0.3\" or \"13.0.4-beta1\".")
    return value


def load(url: str) -> dict:
    """Returns the json-document of the given address."""
    request = urllib.request.Request(url, headers={"Accept-Encoding": "gzip"})
    with urllib.request.urlopen(request, timeout=TIMEOUT_IN_SECONDS) as response:
        content = response.read()
        if response.headers.get("Content-Encoding") == "gzip":
            content = gzip.decompress(content)
    return json.loads(content.decode("utf-8"))


def get_changelog(identifier: str, wanted_version: str) -> dict:
    """Returns what nuget.org publishes as the release-notes of the given version."""
    url = f"{REGISTRATION_URL}/{urllib.parse.quote(identifier.lower(), safe='')}/{urllib.parse.quote(wanted_version.lower(), safe='')}.json"
    leaf = load(url)
    # The entry of the catalog holds the metadata. In this variant of the api it is stated as an address, in others it
    # is part of the answer itself, so both forms are read here.
    catalog_entry = leaf.get("catalogEntry")
    if isinstance(catalog_entry, str):
        catalog_entry = load(catalog_entry)
    if not isinstance(catalog_entry, dict):
        catalog_entry = {}
    release_notes = catalog_entry.get("releaseNotes")
    return {
        "packageId": identifier,
        "version": wanted_version,
        # A package which publishes no release-notes is not an error: it is an answer which says that there are none.
        "releaseNotes": release_notes if release_notes else None,
        "publishedAt": catalog_entry.get("published"),
        "projectUrl": catalog_entry.get("projectUrl"),
        "licenseExpression": catalog_entry.get("licenseExpression"),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Returns the release-notes which one version of one nuget-package publishes.")
    parser.add_argument("--package-id", type=package_id, required=True, help="The id of the package, for example \"Newtonsoft.Json\".")
    parser.add_argument("--version", type=version, required=True, help="The version, for example \"13.0.3\".")
    arguments = parser.parse_args()
    try:
        sys.stdout.write(json.dumps(get_changelog(arguments.package_id, arguments.version), indent=2))
        return 0
    except urllib.error.HTTPError as exception:
        if exception.code == 404:
            sys.stderr.write(f"Nuget.org does not know the version \"{arguments.version}\" of the package \"{arguments.package_id}\".\n")
        else:
            sys.stderr.write(f"Nuget.org answered with the status {exception.code}.\n")
        return 1
    except (urllib.error.URLError, ValueError) as exception:
        sys.stderr.write(f"{exception}\n")
        return 1


if __name__ == "__main__":
    sys.exit(main())
