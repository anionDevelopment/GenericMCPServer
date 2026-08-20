"""Returns where the changelog of one version of one python-package stands, together with what pypi publishes about it.

This script is one of the tools the MCP-server offers. It is called by the BusinessLogicService and answers with a
json-document on stdout. An error is written to stderr and results in an exit-code which is not 0, which the server
turns into a failed tool-call.

Pypi publishes no release-notes of a version, so this tool does not claim to return them: it returns the address which
the package itself states as its changelog (in the project-urls of its metadata) together with the summary and the
point in time of the release. The answer states in "changelogAvailable" whether such an address exists at all, so a
model does not have to guess whether "null" means "no changelog" or "the tool did not find it".

The values of the arguments come from the client of the MCP-server and are therefore untrusted. Each of them is checked
against the format it must have before it is used, which also keeps it from becoming a path-segment of its own inside
the url which is requested.
"""
import argparse
import json
import re
import sys
import urllib.error
import urllib.parse
import urllib.request

API_URL = "https://pypi.org/pypi"
TIMEOUT_IN_SECONDS = 30

# The name of a python-package: letters, digits, hyphens, underscores and dots. Anchored at both ends, so a value which
# contains a slash or a path-segment does not match.
PACKAGE_NAME_REGEX = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._\-]*$")

# A version of a python-package, for example "2.32.3" or "2.0.0rc1".
VERSION_REGEX = re.compile(r"^[0-9]+(\.[0-9]+)*([._\-]?[A-Za-z0-9.\-]+)?$")

# The names under which a package states the address of its changelog. Pypi does not prescribe one of them, so the
# usual ones are looked for - compared without case and without spaces.
NAMES_OF_THE_CHANGELOG = ["changelog", "changes", "releasenotes", "history", "whatsnew"]


def package_name(value: str) -> str:
    """Returns the given value if it is a package-name and raises otherwise."""
    if PACKAGE_NAME_REGEX.match(value) is None:
        raise argparse.ArgumentTypeError(f"\"{value}\" is not a python-package-name. Expected format: letters, digits, hyphens, underscores and dots, for example \"requests\".")
    return value


def version(value: str) -> str:
    """Returns the given value if it is a version and raises otherwise."""
    if VERSION_REGEX.match(value) is None:
        raise argparse.ArgumentTypeError(f"\"{value}\" is not a version. Expected format: numbers which are separated by dots and an optional suffix, for example \"2.32.3\" or \"2.0.0rc1\".")
    return value


def get_address_of_the_changelog(project_urls: dict) -> str:
    """Returns the address which the package states as its changelog, or None if it states none."""
    for name, address in (project_urls or {}).items():
        if name.replace(" ", "").replace("-", "").replace("_", "").lower() in NAMES_OF_THE_CHANGELOG:
            return address
    return None


def get_changelog(name: str, wanted_version: str) -> dict:
    """Returns what pypi publishes about the given version, including where its changelog stands."""
    url = f"{API_URL}/{urllib.parse.quote(name, safe='')}/{urllib.parse.quote(wanted_version, safe='')}/json"
    with urllib.request.urlopen(url, timeout=TIMEOUT_IN_SECONDS) as response:
        answer = json.loads(response.read().decode("utf-8"))
    information = answer.get("info", {})
    address_of_the_changelog = get_address_of_the_changelog(information.get("project_urls"))
    files_of_the_version = answer.get("urls", [])
    return {
        "packageName": information.get("name", name),
        "version": wanted_version,
        "changelogAvailable": address_of_the_changelog is not None,
        "changelog": address_of_the_changelog,
        "summary": information.get("summary"),
        "publishedAt": files_of_the_version[0].get("upload_time_iso_8601") if len(files_of_the_version) > 0 else None,
        "projectUrls": information.get("project_urls"),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Returns where the changelog of one version of one python-package stands.")
    parser.add_argument("--package-name", type=package_name, required=True, help="The name of the package, for example \"requests\".")
    parser.add_argument("--version", type=version, required=True, help="The version, for example \"2.32.3\".")
    arguments = parser.parse_args()
    try:
        sys.stdout.write(json.dumps(get_changelog(arguments.package_name, arguments.version), indent=2))
        return 0
    except urllib.error.HTTPError as exception:
        if exception.code == 404:
            sys.stderr.write(f"Pypi does not know the version \"{arguments.version}\" of the package \"{arguments.package_name}\".\n")
        else:
            sys.stderr.write(f"Pypi answered with the status {exception.code}.\n")
        return 1
    except (urllib.error.URLError, ValueError) as exception:
        sys.stderr.write(f"{exception}\n")
        return 1


if __name__ == "__main__":
    sys.exit(main())
