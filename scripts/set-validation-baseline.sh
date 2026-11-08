#!/usr/bin/env bash
# Sets PackageValidationBaselineVersion in src/Directory.Build.props, so packing fails when a package
# isn't binary compatible with that published version (ADR 0036). Run it after 4.0.0-rc.1 is
# published, with 4.0.0-rc.1, and again after 4.0.0 is published, with 4.0.0.
#
# Usage: scripts/set-validation-baseline.sh <version>
set -euo pipefail

fail() { echo "error: $*" >&2; exit 1; }

[ $# -eq 1 ] || fail "usage: $0 <version>"
version="$1"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-rc\.[1-9][0-9]*)?$ ]] \
  || fail "'$version' isn't a release candidate or final version. Betas may break the API, so they're never a baseline."

props="$(git rev-parse --show-toplevel)/src/Directory.Build.props"

if grep -q '<PackageValidationBaselineVersion>' "$props"; then
  sed -E "s|<PackageValidationBaselineVersion>[^<]*</PackageValidationBaselineVersion>|<PackageValidationBaselineVersion>$version</PackageValidationBaselineVersion>|" \
    "$props" > "$props.tmp"
else
  grep -q '^    <EnablePackageValidation>true</EnablePackageValidation>$' "$props" \
    || fail "EnablePackageValidation isn't where this script expects it in $props."
  sed "s|^    <EnablePackageValidation>true</EnablePackageValidation>$|&\\
    <PackageValidationBaselineVersion>$version</PackageValidationBaselineVersion>|" "$props" > "$props.tmp"
fi
mv "$props.tmp" "$props"

grep -n '<PackageValidationBaselineVersion>' "$props"
echo "Set the baseline to $version. Pack to check it, then commit: git commit -am 'build: validate packages against $version'"
