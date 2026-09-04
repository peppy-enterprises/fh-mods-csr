// SPDX-License-Identifier: MIT

using Fahrenheit.Events;
using System.IO;
using System.Runtime.InteropServices;

using FhXCall = Fahrenheit.FFX.FhCall;

namespace Fahrenheit.Mods.CSR;

[FhLoad(FhGameId.FFX)]
public unsafe class CutsceneRemoverModule : FhModule {
    public static char* get_event_name(int event_id)
        => FhXCall.AtelGetEventName.fnptr!((uint)event_id);

    public delegate void CsrEvent(byte* code_ptr);

    public static readonly Dictionary<string, CsrEvent> removers = new();

    public override bool init(FhModContext mod_context, FileStream global_state_file) {
        Removers.init();

        return FhXCall.AtelEventSetUp.hook(this, csr_event);
    }

    public void csr_event(int event_id) {
        FhXCall.AtelEventSetUp.chain_from(csr_event).fnptr!(event_id);

        string event_name = Marshal.PtrToStringAnsi((nint)get_event_name(event_id))!;
        if (removers.TryGetValue(event_name, out CsrEvent? remover)) {
            _logger.Info($"Remover available for event \"{event_name}\"! Removing cutscenes...");
            byte* code_ptr = Globals.Atel.controllers[0].worker(0)->code_ptr;
            remover(code_ptr);
        }
        else {
            _logger.Debug($"Remover not available for event \"{event_name}\".");
        }
    }
}
