#include <kernel.h>
#include <net.h>
#include <pthread.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <stdarg.h>
#include "bo3_offsets.h"

#pragma comment(lib, "libSceNet_stub_weak.a")

#define LISTEN_PORT 9027
#define GAME_BASE   ((uintptr_t)0x400000)
#define AT(off)     (GAME_BASE + (uintptr_t)(off))

struct Sig { uintptr_t off; uint8_t bytes[8]; };
static const Sig k_write_all_sig = {
    OFF_LiveKeyArchive_WriteAllCategories,
    {0x55,0x48,0x89,0xe5,0x41,0x57,0x41,0x56}
};

static bool g_verified = false;
static int g_client = -1;

static void reply(const char *fmt, ...)
{
    char buf[1024];
    va_list ap;
    va_start(ap, fmt);
    int n = vsnprintf(buf, sizeof(buf) - 2, fmt, ap);
    va_end(ap);
    if (n < 0) return;
    if (n > (int)sizeof(buf) - 2) n = (int)sizeof(buf) - 2;
    buf[n++] = '\n';
    if (g_client >= 0) sceNetSend(g_client, buf, n, 0);
}

static bool verify_build()
{
    const uint8_t *p = (const uint8_t *)AT(k_write_all_sig.off);
    bool ok = memcmp(p, k_write_all_sig.bytes, sizeof(k_write_all_sig.bytes)) == 0;
    g_verified = ok;
    reply(ok ? "CHECK OK entitlement writer matches" : "CHECK FAIL entitlement writer mismatch");
    return ok;
}

static void do_entitlements(int controller)
{
    const int category = 5;
    uint8_t *base = (uint8_t *)AT(OFF_g_keyArchive) + (size_t)KA_STRIDE * controller;
    const int count = 25; // keys 1..25; key 25 is modtools_callingcard
    uint32_t dirty_mask = 0;

    for (int i = 0; i < count; ++i) {
        uint8_t *entry = base + 1536 * category + 16 + 48 * i;
        *(uint8_t *)(entry + 0) = 1;                  // BD_ADD
        *(uint16_t *)(entry + 2) = (uint16_t)(i + 1); // entitlement key
        *(uint64_t *)(entry + 8) = 1;                  // granted
        dirty_mask |= 1u << i;
    }

    *(int *)(base + 9216 + 4 * category) = count;
    *(uint32_t *)(base + 9240 + 4 * category) = dirty_mask;
    ((LiveKeyArchive_WriteAll_t)AT(OFF_LiveKeyArchive_WriteAllCategories))(controller, true);
    reply("ENT queued %d category-5 keys (includes Mod Tools key 25)", count);
}

static void handle(char *cmd)
{
    if (!strcmp(cmd, "PING")) { reply("PONG bo3-entitlements 1"); return; }
    if (!strcmp(cmd, "CHECK")) { verify_build(); return; }
    if (!strcmp(cmd, "STATE")) { reply("STATE verified=%d", g_verified ? 1 : 0); return; }
    if (!g_verified && !verify_build()) { reply("REFUSED build check failed"); return; }
    if (!strcmp(cmd, "UNLOCK_ENT")) { do_entitlements(0); return; }
    reply("ERR unknown command: %s", cmd);
}

static void *server_main(void *)
{
    int listener = sceNetSocket("bo3entitlements", SCE_NET_AF_INET, SCE_NET_SOCK_STREAM, 0);
    if (listener < 0) return nullptr;

    int one = 1;
    sceNetSetsockopt(listener, SCE_NET_SOL_SOCKET, SCE_NET_SO_REUSEADDR, &one, sizeof(one));
    SceNetSockaddrIn addr;
    memset(&addr, 0, sizeof(addr));
    addr.sin_len = sizeof(addr);
    addr.sin_family = SCE_NET_AF_INET;
    addr.sin_port = sceNetHtons(LISTEN_PORT);
    addr.sin_addr.s_addr = 0;
    if (sceNetBind(listener, (SceNetSockaddr *)&addr, sizeof(addr)) < 0 || sceNetListen(listener, 1) < 0) {
        sceNetSocketClose(listener);
        return nullptr;
    }

    for (;;) {
        SceNetSockaddrIn client_addr;
        SceNetSocklen_t client_len = sizeof(client_addr);
        int client = sceNetAccept(listener, (SceNetSockaddr *)&client_addr, &client_len);
        if (client < 0) { sceKernelUsleep(200000); continue; }
        g_client = client;

        char buf[128];
        int len = 0;
        for (;;) {
            char ch;
            int n = sceNetRecv(client, &ch, 1, 0);
            if (n <= 0) break;
            if (ch == '\r') continue;
            if (ch == '\n') {
                buf[len] = 0;
                if (!strcmp(buf, "QUIT")) break;
                if (len) { handle(buf); reply("DONE"); }
                len = 0;
            } else if (len < (int)sizeof(buf) - 1) {
                buf[len++] = ch;
            }
        }
        g_client = -1;
        sceNetSocketClose(client);
    }
    return nullptr;
}

extern "C" {
    int __cdecl module_start(size_t, const void *)
    {
        static ScePthread thread;
        scePthreadCreate(&thread, nullptr, server_main, nullptr, "bo3entitlements");
        return SCE_OK;
    }

    int __cdecl module_stop(size_t, const void *)
    {
        return SCE_OK;
    }
}
