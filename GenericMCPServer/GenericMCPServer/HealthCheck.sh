#!/bin/bash
set -o errexit

port="${HealthCheckPort:-80}"

# The maintenance-health-check-route is provided by GRYLibrary; this backend has no health-controller of its own.
curl --fail -v "http://127.0.0.1:$port/API/Other/Maintenance/HealthCheck"
