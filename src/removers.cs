using Fahrenheit.CoreLib.FFX.Atel;
using System.Runtime.InteropServices;

using static Fahrenheit.Modules.CSR.CSRModule;

namespace Fahrenheit.Modules.CSR;

internal static unsafe partial class Removers {
    [LibraryImport("msvcrt.dll", EntryPoint = "memset", SetLastError = false)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial isize memset(isize dst, i32 value, isize size);

    public static void init() {
        removers.Add("bsil0300", remove_besaid_valley);
        removers.Add("bsil0600", remove_besaid_promontory);
        removers.Add("bsil0700", remove_besaid_village_slope);
    }

    private static void remove(u8* code_ptr, i32 from, i32 to) {
        memset((isize)code_ptr + from, 0, to - from);
    }

    private static void set(u8* code_ptr, i32 offset, u8 value) {
        *(code_ptr + offset) = value;
    }

    private static void set(u8* code_ptr, i32 offset, AtelOpCode opcode) {
        u8* ptr = code_ptr + offset;
        foreach (u8 b in opcode.to_bytes()) {
            *ptr = b;
            ptr++;
        }
    }

    private static void set_tp(u8* code_ptr, i32 offset, u16 x_idx, u16 y_idx, u16 z_idx) {
        u8* ptr = code_ptr + offset;
        set(ptr, 0x0, AtelInst.PUSHF.build(x_idx));
        set(ptr, 0x3, AtelInst.PUSHF.build(y_idx));
        set(ptr, 0x6, AtelInst.PUSHF.build(z_idx));
        set(ptr, 0x9, AtelInst.CALLPOPA.build(0x126));
    }

    internal static void remove_besaid_valley(u8* code_ptr) {
        remove(code_ptr, 0x1B29, 0x1C43); // Wakka pushes Tidus into the water

        remove(code_ptr, 0x3398, 0x33C6); // Initial fadeout into cutscene
        remove(code_ptr, 0x1CFC, 0x1FCF); // Wakka asks Tidus to join the Aurochs

        // Skip the Promontory since it'd be instantly skipped anyway
        // We essentially copy bsil0600:3EB3..3EC8 to bsil0300:1FCF..1FEA
        set(code_ptr, 0x1FCF, AtelInst.PUSHII.build(0x7E)); // GameMoment = 124 -> GameMoment = 126
        set(code_ptr, 0x1FD5, AtelInst.PUSHII.build(0x0F)); // Common.00BB(0) -> Common.00BB(15)
        set(code_ptr, 0x1FE1, AtelInst.PUSHII.build(69));
        set(code_ptr, 0x1FE7, AtelInst.CALL.build(0x11)); // Common.010C(67, 0) -> Common.transitionToMap(69, 0)
    }

    internal static void remove_besaid_promontory(u8* code_ptr) {
        remove(code_ptr, 0x3DB4, 0x3EAD); // Cutscene coming from Valley DEPRECATED
    }

    internal static void remove_besaid_village_slope(u8* code_ptr) {
        remove(code_ptr, 0x264D, 0x289A); // First cutscene

        remove(code_ptr, 0x28B7, 0x28C0); // Don't make the game wait for a fade that won't happen

        set_tp(code_ptr, 0x264D, 0xD, 0xE, 0xF); // Set player position to the vanilla post-cutscene one
    }
}
