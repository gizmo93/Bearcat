#define _GNU_SOURCE
#include <dlfcn.h>
#include <openssl/ssl.h>

SSL *SSL_new(SSL_CTX *context)
{
    static SSL *(*realSslNew)(SSL_CTX *) = NULL;

    if (realSslNew == NULL)
    {
        realSslNew = (SSL *(*)(SSL_CTX *))dlsym(RTLD_NEXT, "SSL_new");
    }

    SSL_CTX_set_session_cache_mode(context, SSL_SESS_CACHE_OFF);
    SSL_CTX_set_options(context, SSL_OP_NO_TICKET);
    return realSslNew(context);
}
