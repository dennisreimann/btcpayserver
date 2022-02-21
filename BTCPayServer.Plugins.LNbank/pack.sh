#!/bin/bash

PUBL=bin/publish
DIST=bin/packed

rm -rf $DIST/*
dotnet publish -c Release -o $PUBL/BTCPayServer.Plugins.LNbank
dotnet run --project ../BTCPayServer.PluginPacker $PUBL/BTCPayServer.Plugins.LNbank BTCPayServer.Plugins.LNbank $DIST
