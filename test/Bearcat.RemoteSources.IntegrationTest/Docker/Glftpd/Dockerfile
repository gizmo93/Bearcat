FROM --platform=linux/amd64 debian:bookworm-slim

ARG GLFTPD_VERSION=2.16_3.6.0
ARG GLFTPD_SHA256=cc225cdeffef27d04cc018a3d82a017467e4617dfae67fb723e2034b56b99190

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl openssl tini xinetd \
    && curl -fsSL -o /tmp/glftpd.tgz "https://glftpd.io/files/glftpd-LNX-${GLFTPD_VERSION}_x64.tgz" \
    && echo "${GLFTPD_SHA256}  /tmp/glftpd.tgz" | sha256sum -c - \
    && tar xzf /tmp/glftpd.tgz -C /tmp \
    && mkdir -p /glftpd \
    && cp -pr "/tmp/glftpd-LNX-${GLFTPD_VERSION}_x64/bin" "/tmp/glftpd-LNX-${GLFTPD_VERSION}_x64/etc" "/tmp/glftpd-LNX-${GLFTPD_VERSION}_x64/ftp-data" /glftpd/ \
    && cp /glftpd/bin/glftpd-full-static /glftpd/bin/glftpd \
    && mkdir -p /glftpd/dev /glftpd/site /glftpd/tmp /glftpd/ftp-data/msgs /glftpd/ftp-data/logs \
    && mknod -m 666 /glftpd/dev/null c 1 3 \
    && mknod -m 666 /glftpd/dev/zero c 1 5 \
    && mknod -m 666 /glftpd/dev/urandom c 1 9 \
    && openssl req -x509 -nodes -newkey ec -pkeyopt ec_paramgen_curve:secp384r1 -days 3650 \
        -subj "/CN=bearcat-glftpd-test" \
        -keyout /tmp/ftpd-key.pem \
        -out /tmp/ftpd-cert.pem \
    && cat /tmp/ftpd-cert.pem /tmp/ftpd-key.pem > /glftpd/ftpd-ecdsa.pem \
    && chmod 600 /glftpd/ftpd-ecdsa.pem \
    && rm -rf /tmp/glftpd.tgz "/tmp/glftpd-LNX-${GLFTPD_VERSION}_x64" /tmp/ftpd-key.pem /tmp/ftpd-cert.pem \
    && apt-get purge -y curl \
    && apt-get autoremove -y \
    && rm -rf /var/lib/apt/lists/*

COPY glftpd.conf.template /etc/glftpd.conf.template
COPY glftpd-user-file /glftpd/ftp-data/users/glftpd
COPY xinetd.conf /etc/xinetd.d/glftpd
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/usr/bin/tini", "--", "/entrypoint.sh"]
