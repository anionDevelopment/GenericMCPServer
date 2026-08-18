"""Returns the details of one CVE from the national vulnerability database.

This script is one of the tools the MCP-server offers. It is called by the BusinessLogicService and answers with a
json-document on stdout. An error is written to stderr and results in an exit-code which is not 0, which the server
turns into a failed tool-call.

The value of every argument comes from the client of the MCP-server and is therefore untrusted. Each of them is
checked against the format it must have before it is used, so a value which is not a CVE-identifier is rejected here
and never reaches the request which is built from it.
"""
import argparse
import json
import re
import sys
import urllib.error
import urllib.parse
import urllib.request

API_URL = "https://services.nvd.nist.gov/rest/json/cves/2.0?cveId="
TIMEOUT_IN_SECONDS = 30

# The format of a CVE-identifier: the literal "CVE", the year and the number of the entry. The expression is anchored
# at both ends, so a value which contains anything else (a path-segment, a quote, a line-break, ...) does not match.
CVE_IDENTIFIER_REGEX = re.compile(r"^CVE-[0-9]{4}-[0-9]{4,7}$")


def cve_identifier(value: str) -> str:
    """Returns the given value if it is a CVE-identifier and raises otherwise. Used as the type of the argument, so
    that argparse rejects a wrong value before the script does anything with it."""
    if CVE_IDENTIFIER_REGEX.match(value) is None:
        raise argparse.ArgumentTypeError(f"\"{value}\" is not a CVE-identifier. Expected format: CVE-<year>-<number>, for example \"CVE-2021-44228\".")
    return value


def get_cve_details(identifier: str) -> dict:
    """Returns the relevant parts of the answer of the national vulnerability database for the given CVE."""
    # The value is quoted although it was validated already: the validation is the guard, the quoting makes sure that
    # the url stays well-formed regardless of what the validation allows.
    request = urllib.request.Request(API_URL + urllib.parse.quote(identifier, safe=""), headers={"Accept": "application/json"})
    with urllib.request.urlopen(request, timeout=TIMEOUT_IN_SECONDS) as response:
        answer = json.loads(response.read().decode("utf-8"))
    vulnerabilities = answer.get("vulnerabilities", [])
    if len(vulnerabilities) == 0:
        raise ValueError(f"The national vulnerability database does not know a CVE with the identifier \"{identifier}\".")
    cve = vulnerabilities[0]["cve"]
    # The answer contains the description in several languages; the english one is the one which is returned.
    descriptions = [entry["value"] for entry in cve.get("descriptions", []) if entry.get("lang") == "en"]
    result = {
        "id": cve.get("id"),
        "published": cve.get("published"),
        "lastModified": cve.get("lastModified"),
        "vulnStatus": cve.get("vulnStatus"),
        "description": descriptions[0] if len(descriptions) > 0 else None,
        "references": [reference.get("url") for reference in cve.get("references", [])],
    }
    # The severity is published in several versions of the CVSS-standard. The newest one which is present is returned,
    # because an older one would understate a vulnerability which was rated again later.
    metrics = cve.get("metrics", {})
    for metric_name in ["cvssMetricV40", "cvssMetricV31", "cvssMetricV30", "cvssMetricV2"]:
        if metric_name in metrics and len(metrics[metric_name]) > 0:
            cvss_data = metrics[metric_name][0].get("cvssData", {})
            result["severity"] = {
                "metric": metric_name,
                "baseScore": cvss_data.get("baseScore"),
                "baseSeverity": cvss_data.get("baseSeverity"),
                "vectorString": cvss_data.get("vectorString"),
            }
            break
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description="Returns the details of one CVE from the national vulnerability database.")
    parser.add_argument("--cve-identifier", type=cve_identifier, required=True, help="The identifier of the CVE, for example \"CVE-2021-44228\".")
    arguments = parser.parse_args()
    try:
        sys.stdout.write(json.dumps(get_cve_details(arguments.cve_identifier), indent=2))
        return 0
    except urllib.error.HTTPError as exception:
        sys.stderr.write(f"The national vulnerability database answered with the status {exception.code}.\n")
        return 1
    except (urllib.error.URLError, ValueError, KeyError) as exception:
        sys.stderr.write(f"{exception}\n")
        return 1


if __name__ == "__main__":
    sys.exit(main())
