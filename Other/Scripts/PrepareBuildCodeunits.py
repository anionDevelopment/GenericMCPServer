from ScriptCollection.TFCPS.TFCPS_Generic import TFCPS_Generic_CLI, TFCPS_Generic_Functions


def prepare_build_codeunits():
    t: TFCPS_Generic_Functions = TFCPS_Generic_CLI().parse(__file__)
    t.tfcps_Tools_General.generate_tasksfile_from_workspace_file(t.repository_folder)
    t.tfcps_Tools_General.generate_codeunits_overview_diagram(t.repository_folder)


if __name__ == "__main__":
    prepare_build_codeunits()
