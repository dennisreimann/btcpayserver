#!/bin/bash

PUBL=bin/publish
DIST=bin/packed

rm -rf $DIST/*
dotnet publish -c Release -o $PUBL/BTCPayServer.Plugins.PodServer
dotnet run --project ../BTCPayServer.PluginPacker $PUBL/BTCPayServer.Plugins.PodServer BTCPayServer.Plugins.PodServer $DIST
