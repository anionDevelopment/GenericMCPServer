from pathlib import Path
from ScriptCollection.ScriptCollectionCore import ScriptCollectionCore
from ScriptCollection.TFCPS.TFCPS_Tools_General import TFCPS_Tools_General


def run_example():
    sc=ScriptCollectionCore()
    t=TFCPS_Tools_General(sc)
    t.start_dockerfile_example(str(Path(__file__).absolute()), True, False, None)
    # HINT the mcp-servers are now available under http://127.0.0.1:/<url-namespace>,
    # for example http://127.0.0.1:1222/GeneralTools (which is the anonymous one)


if __name__ == "__main__":
    run_example()
