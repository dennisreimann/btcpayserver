﻿#!/bin/zsh
set -e

if [ -z "$1" ]; then
  printf "Please provide a version number:\n\n./release.sh 1.0.0\n\n"
  exit 1
fi

# Configuration
version=$1
versionName="v${version}"
remoteName="dennisreimann"
remoteRepo="btcpayserver"
remoteBranch="plugins"
pluginsBranch="lnbank-${versionName}"
pluginsDir=~/Sources/btcpay/plugins
pluginsBuild=bin/packed/BTCPayServer.Plugins.LNbank
pluginsTarget=${pluginsDir}/LNbank
tagName="BTCPayServer.LNbank/${versionName}"
tagDesc="LNbank ${versionName}"

# Cleanup
rm -rf $pluginsBuild

# Parse changelog
changes=$(awk -v ver=${version} '/^#+ \[/ { if (p) { exit }; if ($2 == "["ver"]") { p=1; next} } p' CHANGELOG.md | sed -rz 's/^\n+//; s/\n+$/\n/g')
if [ -z "${changes}" ]; then
  printf "Please provide version details in the CHANGELOG.\n\n"
  exit 1
fi

# We're good, let's roll …
printf "\n\n=====> Update version and package plugin\n\n"
sed -i "s%<AssemblyVersion>.*</AssemblyVersion>%<AssemblyVersion>$version</AssemblyVersion>%g" ./BTCPayServer.Plugins.LNbank.csproj
sed -i "s%<PackageVersion>.*</PackageVersion>%<PackageVersion>$version</PackageVersion>%g" ./BTCPayServer.Plugins.LNbank.csproj
./pack.sh
cd $pluginsBuild

shasums=$(cat "${version}.0/SHA256SUMS")
echo $shasums
notes=$(cat << EOF
${changes}

### SHA256SUMS

\`\`\`
${shasums}
\`\`\`
EOF
)
cd -

printf "\n\n=====> Prepare plugins repo\n\n"
cd ${pluginsDir}
git reset --hard
git checkout master
git pull --rebase
git checkout -b ${pluginsBranch}
cd -

printf "\n\n=====> Copy and commit plugin files\n\n"
mkdir -p ${pluginsTarget}
cp $pluginsBuild/* $pluginsTarget
cd ${pluginsDir}
git add .
git commit -a -m "${tagDesc}"
git push ${remoteName} ${pluginsBranch}

printf "\n\n=====> Create plugin pull request\n\n"
gh pr create --title "${tagDesc}" --body "${changes}"
cd -

printf "\n\n=====> Commit and tag\n\n"
git add .
git commit -a -m "${tagDesc}"
git tag "${tagName}" -a -m "${tagDesc}"
git push ${remoteName} ${remoteBranch}
git push ${remoteName} "refs/tags/${tagName}"

printf "\n\n=====> Create release\n\n"
gh release create "${tagName}" --notes "${notes}" -R "${remoteName}/${remoteRepo}"
