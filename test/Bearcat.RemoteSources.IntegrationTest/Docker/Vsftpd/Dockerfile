FROM alpine:3.22

RUN apk add --no-cache vsftpd openssl tini \
    && mkdir -p /etc/vsftpd /var/run/vsftpd/empty \
    && openssl req -x509 -nodes -newkey rsa:2048 -days 3650 \
        -subj "/CN=bearcat-ftp-test" \
        -keyout /etc/vsftpd/vsftpd.key \
        -out /etc/vsftpd/vsftpd.pem \
    && adduser -D -h /srv/ftp ftpuser \
    && echo "ftpuser:ftppass" | chpasswd

COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/sbin/tini", "--", "/entrypoint.sh"]
