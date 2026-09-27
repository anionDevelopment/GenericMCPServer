from pathlib import Path
from ScriptCollection.ScriptCollectionCore import ScriptCollectionCore
from ScriptCollection.TFCPS.TFCPS_Tools_General import TFCPS_Tools_General


def stop_dockerfile_example():
    sc=ScriptCollectionCore()
    t: TFCPS_Tools_General = TFCPS_Tools_General(sc)
    t.stop_dockerfile_example(str(Path(__file__).absolute()), True, True)


if __name__ == "__main__":
    stop_dockerfile_example()
