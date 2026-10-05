#!/usr/bin/env python3
"""Fails the build when line coverage of the business layers drops below a threshold.

Usage: check-coverage.py <directory with coverage.cobertura.xml files> <minimum percent>

Only the Domain and Application assemblies are measured. The Infrastructure and API
layers are exercised by the integration tests, which run separately against PostgreSQL.
"""
import glob
import sys
import xml.etree.ElementTree as ET

MEASURED = {"Domain", "Application"}

directory, minimum = sys.argv[1], float(sys.argv[2])

covered = valid = 0
for path in glob.glob(f"{directory}/**/coverage.cobertura.xml", recursive=True):
    for package in ET.parse(path).getroot().iter("package"):
        if package.get("name") not in MEASURED:
            continue
        for line in package.iter("line"):
            valid += 1
            if int(line.get("hits", "0")) > 0:
                covered += 1

if valid == 0:
    sys.exit("No coverage data found - did the tests run with --collect:'XPlat Code Coverage'?")

percent = 100.0 * covered / valid
print(f"Domain + Application line coverage: {percent:.1f}% ({covered}/{valid} lines), minimum {minimum:.1f}%")

if percent < minimum:
    sys.exit(f"Coverage {percent:.1f}% is below the required {minimum:.1f}%")
