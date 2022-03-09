#!/bin/bash

PREIMAGE=$(cat /dev/urandom | tr -dc 'a-f0-9' | fold -w 64 | head -n 1)
HASH=`node -e "console.log(require('crypto').createHash('sha256').update(Buffer.from('$PREIMAGE', 'hex')).digest('hex'))"`
./docker-customer-lncli.sh addholdinvoice --memo "hodl invoice" $HASH "$@"

echo "HASH:     $HASH"
echo "PREIMAGE: $PREIMAGE"
echo "SETTLE:   ./docker-customer-lncli.sh settleinvoice $PREIMAGE"
