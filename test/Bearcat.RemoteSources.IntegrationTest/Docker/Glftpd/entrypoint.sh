#!/bin/sh
set -eu

sed \
    -e "s/PASV_ADDRESS/$PASV_ADDRESS/" \
    -e "s/PASV_MIN_PORT/$PASV_MIN_PORT/" \
    -e "s/PASV_MAX_PORT/$PASV_MAX_PORT/" \
    /etc/glftpd.conf.template > /etc/glftpd.conf

mkdir -p "/glftpd/site/Release.One-GROUP/[TEST] - ( 5M 3F - COMPLETE ) - [TEST]"
ln -sfn Release.Two-GROUP "/glftpd/site/(incomplete)-Release.Two-GROUP"
ln -sfn release.one-group.nfo /glftpd/site/Release.One-GROUP/release.one-group.link.nfo

exec xinetd -dontfork -stayalive
