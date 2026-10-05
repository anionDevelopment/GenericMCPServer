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


# The minimal version is optional. The api which is asked here answers with all versions and offers no filter for a
# minimal one, so the filtering happens in this script - the answer of the tool is the same either way.
MINIMAL_VERSION_REGEX = re.compile(r"^[0-9]+(\.[0-9]+)*$")


def minimal_version(value: str) -> str:
    """Returns the given value if it is a version which can be compared and raises otherwise."""
    if MINIMAL_VERSION_REGEX.match(value) is None:
        raise argparse.ArgumentTypeError(f"\"{value}\" is not a usable minimal version. Expected format: numbers which are separated by dots, for example \"2.0\" or \"2.0.1\".")
    return value


def as_comparable(version: str) -> tuple:
    """Returns the numeric parts of a version, so that "1.10" is newer than "1.9"."""
    # Everything behind a hyphen belongs to a pre-release and is not part of the comparison: this script compares which
    # version is newer and does not implement the complete order of a versioning-scheme.
    return tuple(int(part) for part in re.findall(r"[0-9]+", version.split("-")[0]))


def is_at_least(version: str, minimal: str) -> bool:
    """States whether the given version is the minimal one or a newer one."""
    left = as_comparable(version)
    right = as_comparable(minimal)
    length = max(len(left), len(right))
    return left + (0,) * (length - len(left)) >= right + (0,) * (length - len(right))


def only_from(versions: list, minimal: str) -> list:
    """Returns the versions which are the minimal one or newer. Without a minimal one, all of them are returned."""
    if minimal is None:
        return versions
    return [version for version in versions if is_at_least(version, minimal)]


def package_id(value: str) -> str:
    """Returns the given value if it is a nuget-package-id and raises otherwise."""
    if PACKAGE_ID_REGEX.match(value) is None:
        raise argparse.ArgumentTypeError(f"\"{value}\" is not a nuget-package-id. Expected format: letters, digits, dots, hyphens and underscores, for example \"Newtonsoft.Json\".")
    return value


def get_available_versions(identifier: str, minimal: str) -> dict:
    """Returns the versions of the given package which nuget.org publishes."""
    # The flat-container-api is used because it answers with the complete list of the versions of exactly this package.
    # It expects the id in lowercase.
    url = f"{REGISTRY_URL}/{urllib.parse.quote(identifier.lower(), safe='')}/index.json"
    with urllib.request.urlopen(url, timeout=TIMEOUT_IN_SECONDS) as response:
        answer = json.loads(response.read().decode("utf-8"))
    versions = only_from(answer.get("versions", []), minimal)
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
    parser.add_argument("--minimal-version", type=minimal_version, required=False, help="Optional. Only the versions which are this one or newer are returned.")
    arguments = parser.parse_args()
    try:
        sys.stdout.write(json.dumps(get_available_versions(arguments.package_id, arguments.minimal_version), indent=2))
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
