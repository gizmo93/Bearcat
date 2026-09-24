#!/bin/sh
set -eu

config=/etc/vsftpd/vsftpd.conf
listen_port=21

case "$FTP_MODE" in
    plain | explicit | explicit_reuse | explicit_no_resumption) ;;
    implicit) listen_port=990 ;;
    *)
        echo "Unknown FTP_MODE: $FTP_MODE" >&2
        exit 1
        ;;
esac

cat > "$config" <<CONFIG
listen=YES
listen_ipv6=NO
listen_port=$listen_port
background=NO
anonymous_enable=NO
local_enable=YES
write_enable=NO
chroot_local_user=YES
allow_writeable_chroot=YES
secure_chroot_dir=/var/run/vsftpd/empty
seccomp_sandbox=NO
isolate=NO
isolate_network=NO
max_per_ip=0
max_clients=0
pasv_enable=YES
pasv_min_port=$PASV_MIN_PORT
pasv_max_port=$PASV_MAX_PORT
pasv_address=$PASV_ADDRESS
CONFIG

if [ "$FTP_MODE" != "plain" ]; then
    cat >> "$config" <<CONFIG
ssl_enable=YES
rsa_cert_file=/etc/vsftpd/vsftpd.pem
rsa_private_key_file=/etc/vsftpd/vsftpd.key
force_local_logins_ssl=YES
force_local_data_ssl=YES
ssl_ciphers=HIGH
CONFIG
fi

case "$FTP_MODE" in
    explicit) echo "require_ssl_reuse=NO" >> "$config" ;;
    explicit_reuse) echo "require_ssl_reuse=YES" >> "$config" ;;
    explicit_no_resumption)
        echo "require_ssl_reuse=NO" >> "$config"
        export LD_PRELOAD=/usr/lib/disable-session-resumption.so
        ;;
    implicit)
        echo "require_ssl_reuse=NO" >> "$config"
        echo "implicit_ssl=YES" >> "$config"
        ;;
esac

mkdir -p "/srv/ftp/Release.One-GROUP/[TEST] - ( 5M 3F - COMPLETE ) - [TEST]"
ln -sfn Release.Two-GROUP "/srv/ftp/(incomplete)-Release.Two-GROUP"
ln -sfn release.one-group.nfo /srv/ftp/Release.One-GROUP/release.one-group.link.nfo
ln -sfn ../Release.One-GROUP/Subs /srv/ftp/Release.Two-GROUP/Linked.Subs
ln -sfn missing-target /srv/ftp/Broken.Link

exec vsftpd "$config"
