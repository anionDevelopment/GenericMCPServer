"""Returns the versions of one maven-package which are available on maven-central.

This script is one of the tools the MCP-server offers. It is called by the BusinessLogicService and answers with a
json-document on stdout. An error is written to stderr and results in an exit-code which is not 0, which the server
turns into a failed tool-call.

The value of every argument comes from the client of the MCP-server and is therefore untrusted. Each of them is
checked against the format it must have before it is used. That is not only a plausibility-check: the group-id becomes
a part of the path of the url which is requested, so a value which contains a path-segment like ".." would otherwise
address a completely different resource than the package it claims to be.
"""
import argparse
import json
import re
import sys
import urllib.error
import urllib.request
import xml.etree.ElementTree as ElementTree

REPOSITORY_URL = "https://repo1.maven.org/maven2"
TIMEOUT_IN_SECONDS = 30

# A group-id consists of segments which are separated by dots; a segment starts with a letter, a digit or an
# underscore and may additionally contain hyphens. A dot can therefore never stand alone, which is what makes ".."
# impossible. Both expressions are anchored at both ends.
GROUP_ID_REGEX = re.compile(r"^[A-Za-z0-9_][A-Za-z0-9_\-]*(\.[A-Za-z0-9_][A-Za-z0-9_\-]*)*$")
# An artifact-id is one segment which may contain dots, hyphens and underscores but must not start with a dot.
ARTIFACT_ID_REGEX = re.compile(r"^[A-Za-z0-9_][A-Za-z0-9_.\-]*$")


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


def group_id(value: str) -> str:
    """Returns the given value if it is a group-id and raises otherwise."""
    if GROUP_ID_REGEX.match(value) is None:
        raise argparse.ArgumentTypeError(f"\"{value}\" is not a group-id. Expected format: segments separated by dots, for example \"org.apache.logging.log4j\".")
    return value


def artifact_id(value: str) -> str:
    """Returns the given value if it is an artifact-id and raises otherwise."""
    if ARTIFACT_ID_REGEX.match(value) is None:
        raise argparse.ArgumentTypeError(f"\"{value}\" is not an artifact-id. Expected format: letters, digits, dots, hyphens and underscores, for example \"log4j-core\".")
    return value


def get_available_versions(group: str, artifact: str, minimal: str) -> dict:
    """Returns the versions of the given package which maven-central publishes in its metadata."""
    # The metadata-file of the package is used and not the search-api, because it is the authoritative list of the
    # versions of exactly this package and it is not paginated.
    group_path = group.replace(".", "/")
    url = f"{REPOSITORY_URL}/{group_path}/{artifact}/maven-metadata.xml"
    with urllib.request.urlopen(url, timeout=TIMEOUT_IN_SECONDS) as response:
        metadata = ElementTree.fromstring(response.read())
    versioning = metadata.find("versioning")
    if versioning is None:
        raise ValueError(f"The metadata of \"{group}:{artifact}\" does not contain any versioning-information.")
    versions = only_from([element.text for element in versioning.findall("versions/version") if element.text is not None], minimal)
    latest = versioning.find("latest")
    release = versioning.find("release")
    return {
        "groupId": group,
        "artifactId": artifact,
        # "latest" is the newest version of all, "release" is the newest one which is not a snapshot.
        "latest": latest.text if latest is not None else None,
        "release": release.text if release is not None else None,
        "amountOfVersions": len(versions),
        "versions": versions,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Returns the versions of one maven-package which are available on maven-central.")
    parser.add_argument("--group-id", type=group_id, required=True, help="The group-id of the package, for example \"org.apache.logging.log4j\".")
    parser.add_argument("--artifact-id", type=artifact_id, required=True, help="The artifact-id of the package, for example \"log4j-core\".")
    parser.add_argument("--minimal-version", type=minimal_version, required=False, help="Optional. Only the versions which are this one or newer are returned.")
    arguments = parser.parse_args()
    try:
        sys.stdout.write(json.dumps(get_available_versions(arguments.group_id, arguments.artifact_id, arguments.minimal_version), indent=2))
        return 0
    except urllib.error.HTTPError as exception:
        if exception.code == 404:
            sys.stderr.write(f"Maven-central does not know the package \"{arguments.group_id}:{arguments.artifact_id}\".\n")
        else:
            sys.stderr.write(f"Maven-central answered with the status {exception.code}.\n")
        return 1
    except (urllib.error.URLError, ElementTree.ParseError, ValueError) as exception:
        sys.stderr.write(f"{exception}\n")
        return 1


if __name__ == "__main__":
    sys.exit(main())
