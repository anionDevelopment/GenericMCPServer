"""Returns the versions of one python-package which are available on pypi.

This script is one of the tools the MCP-server offers. It is called by the BusinessLogicService and answers with a
json-document on stdout. An error is written to stderr and results in an exit-code which is not 0, which the server
turns into a failed tool-call.

The value of the argument comes from the client of the MCP-server and is therefore untrusted. It is checked against
the format it must have before it is used, which also keeps it from becoming a path-segment of its own inside the url
which is requested.
"""
import argparse
import json
import re
import sys
import urllib.error
import urllib.parse
import urllib.request

REGISTRY_URL = "https://pypi.org/pypi"
TIMEOUT_IN_SECONDS = 30

# The name of a python-package as the packaging-specification allows it: it starts and ends with a letter or a digit
# and may contain dots, hyphens and underscores in between. Anchored at both ends.
PACKAGE_NAME_REGEX = re.compile(r"^[A-Za-z0-9]([A-Za-z0-9._\-]*[A-Za-z0-9])?$")


def package_name(value: str) -> str:
    """Returns the given value if it is a python-package-name and raises otherwise."""
    if PACKAGE_NAME_REGEX.match(value) is None:
        raise argparse.ArgumentTypeError(f"\"{value}\" is not a python-package-name. Expected format: letters, digits, dots, hyphens and underscores, for example \"requests\".")
    return value


def get_available_versions(name: str) -> dict:
    """Returns the versions of the given package which pypi publishes."""
    url = f"{REGISTRY_URL}/{urllib.parse.quote(name, safe='')}/json"
    with urllib.request.urlopen(url, timeout=TIMEOUT_IN_SECONDS) as response:
        answer = json.loads(response.read().decode("utf-8"))
    # "releases" contains every version which was ever uploaded, also the ones whose files were removed afterwards;
    # those are the ones with an empty file-list and they are not offered anymore, so they are left out here.
    releases = answer.get("releases", {})
    versions = [version for version, files_of_version in releases.items() if len(files_of_version) > 0]
    return {
        "packageName": answer.get("info", {}).get("name", name),
        # "info.version" is the version pypi itself considers the current one.
        "latest": answer.get("info", {}).get("version"),
        "amountOfVersions": len(versions),
        "versions": versions,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Returns the versions of one python-package which are available on pypi.")
    parser.add_argument("--package-name", type=package_name, required=True, help="The name of the package, for example \"requests\".")
    arguments = parser.parse_args()
    try:
        sys.stdout.write(json.dumps(get_available_versions(arguments.package_name), indent=2))
        return 0
    except urllib.error.HTTPError as exception:
        if exception.code == 404:
            sys.stderr.write(f"Pypi does not know the package \"{arguments.package_name}\".\n")
        else:
            sys.stderr.write(f"Pypi answered with the status {exception.code}.\n")
        return 1
    except (urllib.error.URLError, ValueError) as exception:
        sys.stderr.write(f"{exception}\n")
        return 1


if __name__ == "__main__":
    sys.exit(main())
