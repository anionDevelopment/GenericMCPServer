"""Returns the versions of one npm-package which are available on the npm-registry.

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

REGISTRY_URL = "https://registry.npmjs.org"
TIMEOUT_IN_SECONDS = 30

# The name of a npm-package, which is written in lowercase and may have a scope ("@scope/name"). The slash of a scoped
# name is part of the name and not a path-separator, which is why it is encoded before it is put into the url.
PACKAGE_NAME_REGEX = re.compile(r"^(@[a-z0-9][a-z0-9._\-]*/)?[a-z0-9][a-z0-9._\-]*$")


def package_name(value: str) -> str:
    """Returns the given value if it is a npm-package-name and raises otherwise."""
    if PACKAGE_NAME_REGEX.match(value) is None:
        raise argparse.ArgumentTypeError(f"\"{value}\" is not a npm-package-name. Expected format: lowercase letters, digits, dots, hyphens and underscores, optionally with a scope, for example \"typescript\" or \"@angular/cli\".")
    return value


def get_available_versions(name: str) -> dict:
    """Returns the versions of the given package which the npm-registry publishes."""
    # The abbreviated document is requested, because the complete one contains the metadata of every single version and
    # is therefore several megabytes big for a package with many versions.
    request = urllib.request.Request(f"{REGISTRY_URL}/{urllib.parse.quote(name, safe='')}", headers={"Accept": "application/vnd.npm.install-v1+json"})
    with urllib.request.urlopen(request, timeout=TIMEOUT_IN_SECONDS) as response:
        answer = json.loads(response.read().decode("utf-8"))
    versions = list(answer.get("versions", {}).keys())
    return {
        "packageName": name,
        # The registry states which version its "latest"-tag points to; that is the one an installation without a
        # version-constraint gets.
        "latest": answer.get("dist-tags", {}).get("latest"),
        "amountOfVersions": len(versions),
        "versions": versions,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Returns the versions of one npm-package which are available on the npm-registry.")
    parser.add_argument("--package-name", type=package_name, required=True, help="The name of the package, for example \"typescript\" or \"@angular/cli\".")
    arguments = parser.parse_args()
    try:
        sys.stdout.write(json.dumps(get_available_versions(arguments.package_name), indent=2))
        return 0
    except urllib.error.HTTPError as exception:
        if exception.code == 404:
            sys.stderr.write(f"The npm-registry does not know the package \"{arguments.package_name}\".\n")
        else:
            sys.stderr.write(f"The npm-registry answered with the status {exception.code}.\n")
        return 1
    except (urllib.error.URLError, ValueError) as exception:
        sys.stderr.write(f"{exception}\n")
        return 1


if __name__ == "__main__":
    sys.exit(main())
