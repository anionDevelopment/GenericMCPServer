from ScriptCollection.TFCPS.Docker.TFCPS_CodeUnitSpecific_Docker import TFCPS_CodeUnitSpecific_Docker_Functions,TFCPS_CodeUnitSpecific_Docker_CLI


def on_building_finished():
    tf:TFCPS_CodeUnitSpecific_Docker_Functions=TFCPS_CodeUnitSpecific_Docker_CLI.parse(__file__)
    # The image is verified over the maintenance-health-check-route of GRYLibrary, because this backend has no
    # health-controller of its own. The server listens on plain HTTP, so no certificate is required for this check.
    tf.verify_image_is_working_via_network(None,{
        "EnforceVerbose":"true"
    },80,"/API/Other/Maintenance/HealthCheck",False,"genericmcpserver_net")


if __name__ == "__main__":
    on_building_finished()
