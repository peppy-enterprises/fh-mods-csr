global using System;
global using Fahrenheit.CoreLib.FFX;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Fahrenheit.CoreLib;
using Fahrenheit.CoreLib.FFX.Atel;

namespace Fahrenheit.Modules.CSR;

public sealed record CSRModuleConfig : FhModuleConfig {
    [JsonConstructor]
    public CSRModuleConfig(string configName, bool configEnabled) : base(configName, configEnabled) { }

    public override bool TrySpawnModule([NotNullWhen(true)] out FhModule? fm) {
        fm = new CSRModule(this);
        return fm.ModuleState == FhModuleState.InitSuccess;
    }
}

public unsafe class CSRModule : FhModule {
    private readonly CSRModuleConfig _moduleConfig;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void AtelEventSetUp(u32 event_id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate char* AtelGetEventName(u32 event_id);

    public static char* get_event_name(u32 event_id)
        => FhUtil.get_fptr<AtelGetEventName>(0x4796e0)(event_id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void TOMkpCrossExtMesFontLClutTypeRGBA(
            u32 p1,
            u8 *text,
            f32 x, f32 y,
            u8 color,
            u8 p6,
            u8 tint_r, u8 tint_g, u8 tint_b, u8 tint_a,
            f32 scale,
            f32 _);

    public static void draw_text_rgba(
            u32 p1,
            u8[] text,
            f32 x, f32 y,
            u8 color,
            u8 p6,
            u8 tint_r, u8 tint_g, u8 tint_b, u8 tint_a,
            f32 scale,
            f32 _) {
        fixed (u8 *_text = text)
        FhUtil.get_fptr<TOMkpCrossExtMesFontLClutTypeRGBA>(0x501700)(p1, _text, x, y, color, p6, tint_r, tint_g, tint_b, tint_a, scale, _);
    }

    public static void draw_text(
            u32 p1,
            u8[] text,
            f32 x, f32 y,
            u8 color,
            u8 p6,
            f32 scale,
            f32 _) {
        draw_text_rgba(p1, text, x, y, color, p6, 0x80, 0x80, 0x80, 0x80, scale, _);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate u32 FUN_0086e990(isize signal_info_ptr);

    private FhMethodHandle<FUN_0086e990> _work_debug;
    private FhMethodHandle<AtelEventSetUp> _csr_event;

    public delegate void CsrEvent(u8* code_ptr);

    public static readonly Dictionary<string, CsrEvent> removers = new();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate u32 AtelCallTargetExec(AtelBasicWorker* work, AtelStack* stack);

    public CSRModule(CSRModuleConfig moduleConfig) : base(moduleConfig) {
        _moduleConfig = moduleConfig;

        _work_debug = new(this, "FFX.exe", work_debug, offset: 0x46e990);
        _csr_event = new(this, "FFX.exe", csr_event, offset: 0x472e90);

        _moduleState  = FhModuleState.InitSuccess;
    }

    public override bool FhModuleInit() {
        Removers.init();

        return _csr_event.hook()
            && _work_debug.hook();
    }

    private u32 work_debug(isize signal_info_ptr) {
        u32 ret = _work_debug.orig_fptr(signal_info_ptr);

        add_work(*(i16*)(signal_info_ptr + 0x4), *(i16*)(signal_info_ptr + 0x10));

        return ret;
    }

    private static void add_work(i16 work_id, i16 entry_id) {
        // Was it already executed recently?
        for (i32 i = 0; i < rew.Count; i++) {
            var o = rew[i];
            if (o.work_id == work_id && o.entry_id == entry_id) {
                o.last_updated = 0;
                return;
            }
        }

        // Can we just add?
        if (rew.Count < 40) {
            rew.Add(new DisplayObject { work_id = work_id, entry_id = entry_id, last_updated = 0 });
            return;
        }

        // Find oldest work to replace
        i32 max = rew[0].last_updated;
        i32 longest_idx = 0;
        for (i32 i = 0; i < rew.Count; i++) {
            var o = rew[i];
            if (o.last_updated > max) {
                max = o.last_updated;
                longest_idx = i;
            }
        }

        rew[longest_idx] = new DisplayObject { work_id = work_id, entry_id = entry_id, last_updated = 0 };
    }

    private struct DisplayObject {
        public i16 work_id;
        public i16 entry_id;
        public i32 last_updated;
    }

    private static List<DisplayObject> rew = new(40);
    public override void render_game() {
        draw_text(0, FhCharset.Us.to_bytes("CSR is running!"), x: 430, y: 5, color: 0x00, 0, scale: 0.5f, 0);

        string event_name = Marshal.PtrToStringAnsi((isize)get_event_name(*(u32*)Globals.event_id))!;
        draw_text(0, FhCharset.Us.to_bytes($"Event: {event_name}"), x: 430, y: 15, color: 0x00, 0, scale: 0.5f, 0);

        draw_text(0, FhCharset.Us.to_bytes($"Map|Spawn: {Globals.save_data->current_room_id}|{Globals.save_data->current_spawnpoint}"),
                  x: 430, y: 25, color: 0x00, 0, scale: 0.5f, 0);

        List<u8> works = new();
        byte[] head = FhCharset.Us.to_bytes("Recent Signal Targets:");

        for (i32 i = 0; i < head.Length; i++) {
            works.Add(head[i]);
        }

        works.Add(0x2);

        const u64 colors = 0x7040603050;
        for (i32 i = 0; i < rew.Count; i++) {
            var o = rew[i];
            if (o.last_updated < 10) {
                works.Add(0xA);
                works.Add((u8)((colors >> (8 * (o.last_updated >> 1))) & 0xFF));
            }

            byte[] work_id = FhCharset.Us.to_bytes($"{o.work_id:X4}:{o.entry_id:X4}, ");
            for (i32 j = 0; j < 10; j++) {
                works.Add(work_id[j]);
            }

            works.Add(0xA);
            works.Add(0x0);

            if (i % 2 == 1) works.Add(0x2);
        }

        draw_text(0, works.ToArray(), x: 430, y: 35, color: 0x00, 0, scale: 0.5f, 0);
    }

    public override void post_update() {
        for (i32 i = 0; i < rew.Count; i++) {
            var o = rew[i];
            o.last_updated++;
        }
    }

    public void csr_event(u32 event_id) {
        _csr_event.orig_fptr(event_id);

        rew.Clear();

        string event_name = Marshal.PtrToStringAnsi((isize)get_event_name(event_id))!;
        if (removers.TryGetValue(event_name, out CsrEvent? remover)) {
            FhLog.Info($"Remover available for event \"{event_name}\"");
            u8* code_ptr = Globals.Atel.controllers[0].worker(0)->code_ptr;
            remover(code_ptr);
        } else {
            FhLog.Info($"Remover not available for event \"{event_name}\"");
        }
    }
}
