"""Returns the versions of one nuget-package which are available on nuget.org.

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

REGISTRY_URL = "https://api.nuget.org/v3-flatcontainer"
TIMEOUT_IN_SECONDS = 30

# The id of a nuget-package: letters, digits, dots, hyphens and underscores, not starting with a dot. Anchored at both
# ends, so a value which contains a slash or a path-segment does not match.
PACKAGE_ID_REGEX = re.compile(r"^[A-Za-z0-9_][A-Za-z0-9_.\-]*$")


def package_id(value: str) -> str:
    """Returns the given value if it is a nuget-package-id and raises otherwise."""
    if PACKAGE_ID_REGEX.match(value) is None:
        raise argparse.ArgumentTypeError(f"\"{value}\" is not a nuget-package-id. Expected format: letters, digits, dots, hyphens and underscores, for example \"Newtonsoft.Json\".")
    return value


def get_available_versions(identifier: str) -> dict:
    """Returns the versions of the given package which nuget.org publishes."""
    # The flat-container-api is used because it answers with the complete list of the versions of exactly this package.
    # It expects the id in lowercase.
    url = f"{REGISTRY_URL}/{urllib.parse.quote(identifier.lower(), safe='')}/index.json"
    with urllib.request.urlopen(url, timeout=TIMEOUT_IN_SECONDS) as response:
        answer = json.loads(response.read().decode("utf-8"))
    versions = answer.get("versions", [])
    # The api answers with the versions in ascending order, so the last one is the newest one. A version which contains
    # a hyphen is a pre-release, so the newest one without a hyphen is the newest released version.
    released_versions = [version for version in versions if "-" not in version]
    return {
        "packageId": identifier,
        "latest": versions[-1] if len(versions) > 0 else None,
        "release": released_versions[-1] if len(released_versions) > 0 else None,
        "amountOfVersions": len(versions),
        "versions": versions,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Returns the versions of one nuget-package which are available on nuget.org.")
    parser.add_argument("--package-id", type=package_id, required=True, help="The id of the package, for example \"Newtonsoft.Json\".")
    arguments = parser.parse_args()
    try:
        sys.stdout.write(json.dumps(get_available_versions(arguments.package_id), indent=2))
        return 0
    except urllib.error.HTTPError as exception:
        if exception.code == 404:
            sys.stderr.write(f"Nuget.org does not know the package \"{arguments.package_id}\".\n")
        else:
            sys.stderr.write(f"Nuget.org answered with the status {exception.code}.\n")
        return 1
    except (urllib.error.URLError, ValueError) as exception:
        sys.stderr.write(f"{exception}\n")
        return 1


if __name__ == "__main__":
    sys.exit(main())
