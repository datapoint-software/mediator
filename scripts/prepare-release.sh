#!/usr/bin/env bash
# Prepares a release on main: checks the repository, moves each PublicAPI.Unshipped.txt into its
# PublicAPI.Shipped.txt, and adds the version's section to CHANGELOG.md with git-cliff.
# It doesn't commit, tag or push. See "Releasing" in CONTRIBUTING.md.
#
# Usage: scripts/prepare-release.sh <version>    for example 4.0.0-beta.8
set -euo pipefail

fail() { echo "error: $*" >&2; exit 1; }

[ $# -eq 1 ] || fail "usage: $0 <version>"
version="$1"
root="$(git rev-parse --show-toplevel)"
cd "$root"

# Versions follow Semantic Versioning 2.0.0, with no tag prefix, and pre-releases are alpha.N, beta.N or rc.N (ADR 0036).
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-(alpha|beta|rc)\.[1-9][0-9]*)?$ ]] \
  || fail "'$version' isn't a version such as 4.0.0-beta.8, 4.0.0-rc.1 or 4.0.0."

command -v git-cliff > /dev/null || fail "git-cliff isn't installed: brew install git-cliff"
[ "$(git branch --show-current)" = "main" ] || fail "releases are prepared on main."
[ -z "$(git status --porcelain)" ] || fail "the working tree has changes. Commit or stash them first."

git fetch --quiet --tags origin main
[ "$(git rev-parse HEAD)" = "$(git rev-parse origin/main)" ] || fail "main isn't the same as origin/main. Pull or push first."
! git rev-parse --quiet --verify "refs/tags/$version" > /dev/null || fail "the tag $version already exists."

previous="$(git describe --tags --abbrev=0)"
latest="$(printf '%s\n%s\n' "$previous" "$version" | sort -V | tail -n 1)"
# sort -V puts 4.0.0 before 4.0.0-rc.1, so a final version after its own pre-releases needs its own check.
if [ "$latest" != "$version" ] && [ "${previous%%-*}" != "$version" ]; then
  fail "$version doesn't come after the last release, $previous."
fi

# Moves the unshipped API into the shipped API. A '*REMOVED*' line removes the line it names.
header='#nullable enable'
for unshipped in src/*/PublicAPI.Unshipped.txt; do
  shipped="$(dirname "$unshipped")/PublicAPI.Shipped.txt"
  [ -f "$shipped" ] || fail "$shipped is missing."

  entries="$(grep -vxF "$header" "$unshipped" | grep -v '^[[:space:]]*$' || true)"
  [ -n "$entries" ] || continue

  removed="$(printf '%s\n' "$entries" | sed -n 's/^\*REMOVED\*//p')"
  added="$(printf '%s\n' "$entries" | grep -v '^\*REMOVED\*' || true)"
  current="$(grep -vxF "$header" "$shipped" | grep -v '^[[:space:]]*$' || true)"

  if [ -n "$removed" ]; then
    while IFS= read -r line; do
      printf '%s\n' "$current" | grep -qxF -- "$line" || fail "$unshipped removes a line that isn't in $shipped: $line"
    done <<< "$removed"
    current="$(printf '%s\n' "$current" | grep -vxF -f <(printf '%s\n' "$removed") || true)"
  fi

  {
    echo "$header"
    # Ordinal sort, as the analyzer's code fix writes the files.
    printf '%s\n%s\n' "$current" "$added" | grep -v '^$' | LC_ALL=C sort -u
  } > "$shipped.tmp"
  mv "$shipped.tmp" "$shipped"
  echo "$header" > "$unshipped"
  echo "Moved the unshipped API of $(basename "$(dirname "$unshipped")")."
done

git cliff --unreleased --tag "$version" --prepend CHANGELOG.md
echo
echo "Prepared $version. Next:"
echo "  1. Review the new section at the top of CHANGELOG.md."
echo "  2. Build and test, then commit: git commit -am 'chore(release): $version'"
echo "  3. Push, wait for CI, then tag: git tag $version && git push origin $version"
