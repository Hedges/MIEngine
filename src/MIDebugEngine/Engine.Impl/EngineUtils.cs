// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.Debugger.Interop;
using System.Diagnostics;
using System.Globalization;
using MICore;
using System.Threading.Tasks;
using System.Linq;

namespace Microsoft.MIDebugEngine
{
    public static class EngineUtils
    {
        private static TargetArchitecture arch;

        internal static string AsAddr(ulong addr, bool is64bit)
        {
            string addrFormat = is64bit ? "x16" : "x8";
            return "0x" + addr.ToString(addrFormat, CultureInfo.InvariantCulture);
        }

        internal static string GetAddressDescription(DebuggedProcess proc, ulong ip)
        {
            string description = null;
            try
            {
                proc.WorkerThread.RunOperation(async () =>
                {
                    description = await EngineUtils.GetAddressDescriptionAsync(proc, ip);
                }
                );
                if(description.Equals("??", StringComparison.Ordinal))
                {
                    description = AsAddr(ip, proc.Is64BitArch);
                }
            }
            catch(MIException e)
            {
                description = AsAddr(ip, proc.Is64BitArch);
            }
            return description;
        }

        internal static async Task<string> GetAddressDescriptionAsync(DebuggedProcess proc, ulong ip)
        {
            string location = null;
            IEnumerable<DisasmInstruction> instructions = await proc.Disassembly.FetchInstructions(ip, 1);
            if(instructions != null)
            {
                foreach(DisasmInstruction instruction in instructions)
                {
                    if(location == null && !String.IsNullOrEmpty(instruction.Symbol))
                    {
                        location = instruction.Symbol;
                        break;
                    }
                }
            }

            if(location == null)
            {
                string addrFormat = proc.Is64BitArch ? "x16" : "x8";
                location = "0x" + ip.ToString(addrFormat, CultureInfo.InvariantCulture);
            }

            return location;
        }


        public static void CheckOk(int hr)
        {
            if(hr != 0)
            {
                throw new MIException(hr);
            }
        }

        public static void RequireOk(int hr)
        {
            if(hr != 0)
            {
                throw new InvalidOperationException();
            }
        }

        public static AD_PROCESS_ID GetProcessId(IDebugProcess2 process)
        {
            AD_PROCESS_ID[] pid = new AD_PROCESS_ID[1];
            EngineUtils.RequireOk(process.GetPhysicalProcessId(pid));
            return pid[0];
        }

        public static int UnexpectedException(Exception e)
        {
            Debug.Fail("Unexpected exception.");
            return Constants.RPC_E_SERVERFAULT;
        }

        internal static bool IsFlagSet(uint value, int flagValue)
        {
            return (value & flagValue) != 0;
        }

        internal static bool ProcIdEquals(AD_PROCESS_ID pid1, AD_PROCESS_ID pid2)
        {
            if(pid1.ProcessIdType != pid2.ProcessIdType)
            {
                return false;
            }
            else if(pid1.ProcessIdType == (int)enum_AD_PROCESS_ID.AD_PROCESS_ID_SYSTEM)
            {
                return pid1.dwProcessId == pid2.dwProcessId;
            }
            else
            {
                return pid1.guidProcessId == pid2.guidProcessId;
            }
        }

        public static void SetTargetArch(TargetArchitecture _arch)
        {
            arch = _arch;
        }

        //
        // The RegisterNameMap maps register names to logical group names. The architecture of 
        // the platform is described with all its varients. Any particular target may only contains a subset 
        // of the available registers.
        public class RegisterNameMap
        {
            private Entry[] _map;
            private struct Entry
            {
                public readonly string Name;
                public readonly bool IsRegex;
                public readonly string Group;
                public Entry(string name, bool isRegex, string group)
                {
                    Name = name;
                    IsRegex = isRegex;
                    Group = group;
                }
            };

            private static readonly Entry[] s_arm32Registers = new Entry[]
            {
                new Entry( "amx.+", true, "Apple AMX"),
                new Entry( "sp", false, "CPU"),
                new Entry( "lr", false, "CPU"),
                new Entry( "pc", false, "CPU"),
                new Entry( "cpsr", false, "CPU"),
                new Entry( "r[0-9]+", true, "CPU"),
                new Entry( "x[0-9]+", true, "CPU"),
                new Entry( "fpscr", false, "FPU"),
                new Entry( "f[0-9]+", true, "FPU"),
                new Entry( "s[0-9]+", true, "IEEE Single"),
                new Entry( "d[0-9]+", true, "IEEE Double"),
                new Entry( "q[0-9]+", true, "NEON"),
                new Entry( "v[0-9]+", true, "Vector"),
            };

            private static readonly Entry[] s_X86Registers = new Entry[]
            {
                new Entry( "rax", false, "CPU" ),
                new Entry( "rbx", false, "CPU" ),
                new Entry( "rcx", false, "CPU" ),
                new Entry( "rdx", false, "CPU" ),
                new Entry( "rsi", false, "CPU" ),
                new Entry( "rdi", false, "CPU" ),
                new Entry( "rbp", false, "CPU" ),
                new Entry( "rsp", false, "CPU" ),
                new Entry( "rip", false, "CPU" ),
                new Entry( "r[0-9]+", true, "CPU"),
                new Entry( "eax", false, "CPU" ),
                new Entry( "ebx", false, "CPU" ),
                new Entry( "ecx", false, "CPU" ),
                new Entry( "edx", false, "CPU" ),
                new Entry( "esi", false, "CPU" ),
                new Entry( "edi", false, "CPU" ),
                new Entry( "esp", false, "CPU" ),
                new Entry( "ebp", false, "CPU" ),
                new Entry( "eip", false, "CPU" ),
                new Entry( "eflags", false, "CPU" ),
                new Entry( "cs", false, "CPU" ),
                new Entry( "ds", false, "CPU" ),
                new Entry( "ss", false, "CPU" ),
                new Entry( "es", false, "CPU" ),
                new Entry( "fs", false, "CPU" ),
                new Entry( "gs", false, "CPU" ),
                new Entry( "st", true, "CPU" ),
                new Entry( "fctrl", false, "CPU" ),
                new Entry( "fstat", false, "CPU" ),
                new Entry( "ftag", false, "CPU" ),
                new Entry( "fiseg", false, "CPU" ),
                new Entry( "fioff", false, "CPU" ),
                new Entry( "foseg", false, "CPU" ),
                new Entry( "fooff", false, "CPU" ),
                new Entry( "fop", false, "CPU" ),
                new Entry( "mxcsr", false, "CPU" ),
                new Entry( "orig_eax", false, "CPU" ),
                new Entry( "al", false, "CPU" ),
                new Entry( "bl", false, "CPU" ),
                new Entry( "cl", false, "CPU" ),
                new Entry( "dl", false, "CPU" ),
                new Entry( "ah", false, "CPU" ),
                new Entry( "bh", false, "CPU" ),
                new Entry( "ch", false, "CPU" ),
                new Entry( "dh", false, "CPU" ),
                new Entry( "ax", false, "CPU" ),
                new Entry( "bx", false, "CPU" ),
                new Entry( "cx", false, "CPU" ),
                new Entry( "dx", false, "CPU" ),
                new Entry( "si", false, "CPU" ),
                new Entry( "di", false, "CPU" ),
                new Entry( "bp", false, "CPU" ),
                new Entry( "sp", false, "CPU" ),
                new Entry( "mm[0-7]", true, "MMX" ),
                new Entry( "xmm[0-7]ih", true, "SSE2" ),
                new Entry( "xmm[0-7]il", true, "SSE2" ),
                new Entry( "xmm[0-7]dh", true, "SSE2" ),
                new Entry( "xmm[0-7]dl", true, "SSE2" ),
                new Entry( "xmm[0-7][0-7]", true, "SSE" ),
                new Entry( "ymm.+", true, "AVX" ),
                new Entry( "mm[0-7][0-7]", true, "AMD3DNow" ),
            };

            private static readonly Entry[] s_X64Registers = new Entry[]
            {
                new Entry( "rax", false, "CPU" ),
                new Entry( "rbx", false, "CPU" ),
                new Entry( "rcx", false, "CPU" ),
                new Entry( "rdx", false, "CPU" ),
                new Entry( "rsi", false, "CPU" ),
                new Entry( "rdi", false, "CPU" ),
                new Entry( "rbp", false, "CPU" ),
                new Entry( "rsp", false, "CPU" ),
                new Entry( "r8", false, "CPU" ),
                new Entry( "r9", false, "CPU" ),
                new Entry( "r10", false, "CPU" ),
                new Entry( "r11", false, "CPU" ),
                new Entry( "r12", false, "CPU" ),
                new Entry( "r13", false, "CPU" ),
                new Entry( "r14", false, "CPU" ),
                new Entry( "r15", false, "CPU" ),
                new Entry( "rip", false, "CPU" ),
                new Entry( "eflags", false, "CPU" ),
                new Entry( "cs", false, "CPU Segments" ),
                new Entry( "ds", false, "CPU Segments" ),
                new Entry( "es", false, "CPU Segments" ),
                new Entry( "ss", false, "CPU Segments" ),
                new Entry( "fs", false, "CPU Segments" ),
                new Entry( "gs", false, "CPU Segments" ),
                new Entry( "st[0-7]", true, "Floating Point" ),
                new Entry( "xmm[0-9]+", true, "SSE" ),
                new Entry( "ymm.+", true, "AVX" ),
            };

            private static readonly Entry[] s_allRegisters = new Entry[]
            {
                    new Entry( ".+", true, "CPU"),
            };

            public static RegisterNameMap Create(string[] registerNames)
            {
                RegisterNameMap map = new RegisterNameMap();
                switch(arch)
                {
                    case TargetArchitecture.ARM:
                    case TargetArchitecture.ARM64:
                        map._map = s_arm32Registers;
                        break;

                    case TargetArchitecture.X86:
                        map._map = s_X86Registers;
                        break;

                    case TargetArchitecture.X64:
                        map._map = s_X64Registers;
                        break;

                    default:
                        map._map = s_allRegisters;
                        break;
                }
                return map;
            }

            public string GetGroupName(string regName)
            {
                foreach(var e in _map)
                {
                    if(e.IsRegex)
                    {
                        if(System.Text.RegularExpressions.Regex.IsMatch(regName, e.Name))
                        {
                            return e.Group;
                        }
                    }
                    else if(e.Name == regName)
                    {
                        return e.Group;
                    }
                }
                return "Other Registers";
            }
        };

        internal static string GetExceptionDescription(Exception exception)
        {
            if(!ExceptionHelper.IsCorruptingException(exception))
            {
                return exception.Message;
            }
            else
            {
                return string.Format(CultureInfo.CurrentCulture, MICoreResources.Error_CorruptingException, exception.GetType().FullName, exception.StackTrace);
            }
        }

        internal class SignalMap : Dictionary<string, uint>
        {
            private static SignalMap s_instance;
            private SignalMap()
            {
                this["SIGHUP"] = 1;
                this["SIGINT"] = 2;
                this["SIGQUIT"] = 3;
                this["SIGILL"] = 4;
                this["SIGTRAP"] = 5;
                this["SIGABRT"] = 6;
                this["SIGIOT"] = 6;
                this["SIGBUS"] = 7;
                this["SIGFPE"] = 8;
                this["SIGKILL"] = 9;
                this["SIGUSR1"] = 10;
                this["SIGSEGV"] = 11;
                this["SIGUSR2"] = 12;
                this["SIGPIPE"] = 13;
                this["SIGALRM"] = 14;
                this["SIGTERM"] = 15;
                this["SIGSTKFLT"] = 16;
                this["SIGCHLD"] = 17;
                this["SIGCONT"] = 18;
                this["SIGSTOP"] = 19;
                this["SIGTSTP"] = 20;
                this["SIGTTIN"] = 21;
                this["SIGTTOU"] = 22;
                this["SIGURG"] = 23;
                this["SIGXCPU"] = 24;
                this["SIGXFSZ"] = 25;
                this["SIGVTALRM"] = 26;
                this["SIGPROF"] = 27;
                this["SIGWINCH"] = 28;
                this["SIGIO"] = 29;
                this["SIGPOLL"] = 29;
                this["SIGPWR"] = 30;
                this["SIGSYS"] = 31;
                this["SIGUNUSED"] = 31;
            }
            public static SignalMap Instance
            {
                get
                {
                    if(s_instance == null)
                    {
                        s_instance = new SignalMap();
                    }
                    return s_instance;
                }
            }
        }
    }
}
