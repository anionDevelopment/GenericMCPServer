import argparse
from pathlib import Path
from ScriptCollection.SCLog import LogLevel
from ScriptCollection.GeneralUtilities import GeneralUtilities
from ScriptCollection.ScriptCollectionCore import ScriptCollectionCore
from ScriptCollection.TFCPS.TFCPS_Tools_General import TFCPS_Tools_General


def start_dockerfile_example():
    parser = argparse.ArgumentParser()
    parser.add_argument("-v", "--verbose", action="store_true", default=False)
    args = parser.parse_args()
    current_file = str(Path(__file__).absolute())
    env_file_name = "Variables.env"
    sc=ScriptCollectionCore()
    sc.log.loglevel = LogLevel.Debug if args.verbose else LogLevel.Information
    t: TFCPS_Tools_General = TFCPS_Tools_General(sc)
    repository_folder:str=GeneralUtilities.resolve_relative_path("../../../../../../..",current_file)
    t.ensure_env_file_is_generated(current_file, env_file_name, dict({
        'InitialAdminPassword': 'admin',
        'InitialDatabaseType': 'PostgreSQL',
        'InitialDatabaseConnectionString': 'Host=consurv_database;Port=5432;Username=user;Password=pa55w0rd;Database=ConSurvDatabase;IncludeErrorDetail=true;',
        'InitialCameraAddresses': 'rtsp://tpuser:GgrechuH_fzing655f@192.168.1.141/stream1',
        "image_postgresql":t.oci_image_manager.get_registry_address_for_image_with_default_tag(repository_folder,"PostgreSQL"),
        "image_adminer":t.oci_image_manager.get_registry_address_for_image_with_default_tag(repository_folder,"Adminer"),
    }))
    t.start_dockerfile_example(current_file, True, True, env_file_name)


if __name__ == "__main__":
    start_dockerfile_example()

