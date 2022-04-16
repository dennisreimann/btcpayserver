#!/bin/bash

PUBL=bin/publish
DIST=bin/packed

rm -rf $DIST/*
dotnet publish -c Release -o $PUBL/BTCPayServer.Plugins.Buchhalter
dotnet run --project ../BTCPayServer.PluginPacker $PUBL/BTCPayServer.Plugins.Buchhalter BTCPayServer.Plugins.Buchhalter $DIST
