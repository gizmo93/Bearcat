FROM alpine:3.22 AS build

RUN apk add --no-cache gcc musl-dev openssl-dev
COPY disable-session-resumption.c /src/disable-session-resumption.c
RUN gcc -shared -fPIC -O2 -o /src/disable-session-resumption.so /src/disable-session-resumption.c -lssl

FROM alpine:3.22

RUN apk add --no-cache vsftpd openssl tini \
    && mkdir -p /etc/vsftpd /var/run/vsftpd/empty \
    && openssl req -x509 -nodes -newkey rsa:2048 -days 3650 \
        -subj "/CN=bearcat-ftp-test" \
        -keyout /etc/vsftpd/vsftpd.key \
        -out /etc/vsftpd/vsftpd.pem \
    && adduser -D -h /srv/ftp ftpuser \
    && echo "ftpuser:ftppass" | chpasswd

COPY --from=build /src/disable-session-resumption.so /usr/lib/disable-session-resumption.so
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/sbin/tini", "--", "/entrypoint.sh"]
