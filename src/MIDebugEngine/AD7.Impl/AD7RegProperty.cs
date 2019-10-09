// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.VisualStudio.Debugger.Interop;
using System.Diagnostics;

namespace Microsoft.MIDebugEngine
{
    public class AD7RegProperty : IDebugProperty2
    {
        private readonly AD7Engine _engine;
        private readonly RegisterDescription _reg;
        private DEBUG_PROPERTY_INFO _info;
        public AD7RegProperty(AD7Engine engine, RegisterDescription reg, DEBUG_PROPERTY_INFO info)
        {
            _engine = engine;
            _reg = reg;
            _info = info;
        }

        public int EnumChildren(enum_DEBUGPROP_INFO_FLAGS dwFields, uint dwRadix, ref Guid guidFilter, enum_DBG_ATTRIB_FLAGS dwAttribFilter, string pszNameFilter, uint dwTimeout, out IEnumDebugPropertyInfo2 ppEnum)
        {
            ppEnum = null;
            return Constants.S_FALSE;
        }

        public int GetDerivedMostProperty(out IDebugProperty2 ppDerivedMost)
        {
            throw new NotImplementedException();
        }

        public int GetExtendedInfo(ref Guid guidExtendedInfo, out object pExtendedInfo)
        {
            throw new NotImplementedException();
        }

        public int GetMemoryBytes(out IDebugMemoryBytes2 ppMemoryBytes)
        {
            throw new NotImplementedException();
        }

        public int GetMemoryContext(out IDebugMemoryContext2 ppMemory)
        {
            throw new NotImplementedException();
        }

        public int GetParent(out IDebugProperty2 ppParent)
        {
            throw new NotImplementedException();
        }

        public int GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS dwFields, uint dwRadix, uint dwTimeout, IDebugReference2[] rgpArgs, uint dwArgCount, DEBUG_PROPERTY_INFO[] pPropertyInfo)
        {
            pPropertyInfo[0] = _info;
            rgpArgs = null;

            return Constants.S_OK;
        }

        public int GetReference(out IDebugReference2 ppReference)
        {
            throw new NotImplementedException();
        }

        public int GetSize(out uint pdwSize)
        {
            throw new NotImplementedException();
        }

        public int SetValueAsReference(IDebugReference2[] rgpArgs, uint dwArgCount, IDebugReference2 pValue, uint dwTimeout)
        {
            throw new NotImplementedException();
        }

        public int SetValueAsString(string pszValue, uint dwRadix, uint dwTimeout)
        {
            string expr = "$"+_reg.Name+"="+pszValue;
            string result = null;
            _engine.DebuggedProcess.WorkerThread.RunOperation(async () =>
            {
                result = await _engine.DebuggedProcess.MICommandFactory.DataEvaluateExpression(expr, _engine.DebuggedProcess.MICommandFactory.CurrentThread, 0);
            });
            if (result != null)
            {
                _info.bstrValue = pszValue;
                if(_reg.Name == "pc")
                {
                    _engine.DebuggedProcess.ThreadCache.MarkDirty();
                }
                return Constants.S_OK;
            }
            return Constants.S_FALSE;
        }
    }

    public class AD7RegGroupProperty : IDebugProperty2
    {
        private readonly AD7Engine _engine;
        private readonly RegisterGroup _group;
        private readonly Tuple<int, string>[] _values;
        public readonly DEBUG_PROPERTY_INFO PropertyInfo;
        public AD7RegGroupProperty(AD7Engine engine, enum_DEBUGPROP_INFO_FLAGS dwFields, RegisterGroup grp, Tuple<int, string>[] values)
        {
            _engine = engine;
            _group = grp;
            _values = values;
            PropertyInfo = CreateInfo(dwFields);
        }

        private DEBUG_PROPERTY_INFO CreateInfo(enum_DEBUGPROP_INFO_FLAGS dwFields)
        {
            DEBUG_PROPERTY_INFO info = new DEBUG_PROPERTY_INFO();
            info.dwFields = 0;
            if ((dwFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME) != 0)
            {
                info.bstrName = _group.Name;
                info.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME;
            }

            if ((dwFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB) != 0)
            {
                info.dwAttrib = 0;
                info.dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_READONLY;
                info.dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_OBJ_IS_EXPANDABLE;
                info.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB;
            }

            if ((dwFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP) != 0)
            {
                info.pProperty = this;
                info.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP;
            }

            return info;
        }

        public int EnumChildren(enum_DEBUGPROP_INFO_FLAGS dwFields, uint dwRadix, ref Guid guidFilter, enum_DBG_ATTRIB_FLAGS dwAttribFilter, string pszNameFilter, uint dwTimeout, out IEnumDebugPropertyInfo2 ppEnum)
        {
            DEBUG_PROPERTY_INFO[] properties = new DEBUG_PROPERTY_INFO[_group.Count];
            int i = 0;
            foreach (var reg in _engine.DebuggedProcess.GetRegisterDescriptions())
            {
                if (reg.Group == _group)
                {
                    properties[i].dwFields = 0;
                    if ((dwFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME) != 0)
                    {
                        properties[i].bstrName = reg.Name;
                        properties[i].dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME;
                    }
                    if ((dwFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE) != 0)
                    {
                        var desc = Array.Find(_values, (v) => { return v.Item1 == reg.Index; });
                        properties[i].bstrValue = desc == null ? "??" : desc.Item2;
                        if (reg.Group.Name == "CPU")
                        {
                            ulong val = Convert.ToUInt64(properties[i].bstrValue, 16);
                            properties[i].bstrValue = EngineUtils.AsAddr(val, _engine.DebuggedProcess.Is64BitArch && (reg.Name != "cpsr"));
                        }
                        else if (reg.Group.Name == "IEEE Single")
                        {
                            int beg = 0, end = properties[i].bstrValue.Length;
                            if(_engine.DebuggedProcess.Is64BitArch)
                            {
                                beg = properties[i].bstrValue.IndexOf("s = 0x", StringComparison.Ordinal) + "s = ".Length;
                                end = properties[i].bstrValue.LastIndexOf("}", StringComparison.Ordinal);
                            }
                            if (end > beg)
                            {
                                properties[i].bstrValue = properties[i].bstrValue.Substring(beg, end - beg);
                                Int32 hex = Convert.ToInt32(properties[i].bstrValue, 16);
                                float val = BitConverter.ToSingle(BitConverter.GetBytes(hex), 0);
                                properties[i].bstrValue = val.ToString("G9", CultureInfo.InvariantCulture);
                            }
                        }
                        properties[i].dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE;
                    }
                    if ((dwFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB) != 0)
                    {
                        properties[i].dwAttrib = 0;
                        if (reg.Group.Name != "CPU")
                        {
                            properties[i].dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_READONLY;
                            if (reg.Group.Name == "IEEE Single")
                            {
                                properties[i].dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_RAW_STRING;
                            }
                        }
                        properties[i].dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB;
                    }
                    if ((dwFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP) != 0)
                    {
                        properties[i].pProperty = new AD7RegProperty(_engine, reg, properties[i]);
                        properties[i].dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP;
                    }
                    i++;
                }
            }
            Debug.Assert(i == _group.Count, "Failed to find registers in group.");
            ppEnum = new AD7PropertyEnum(properties);
            return Constants.S_OK;
        }

        public int GetDerivedMostProperty(out IDebugProperty2 ppDerivedMost)
        {
            throw new NotImplementedException();
        }

        public int GetExtendedInfo(ref Guid guidExtendedInfo, out object pExtendedInfo)
        {
            throw new NotImplementedException();
        }

        public int GetMemoryBytes(out IDebugMemoryBytes2 ppMemoryBytes)
        {
            throw new NotImplementedException();
        }

        public int GetMemoryContext(out IDebugMemoryContext2 ppMemory)
        {
            throw new NotImplementedException();
        }

        public int GetParent(out IDebugProperty2 ppParent)
        {
            throw new NotImplementedException();
        }

        public int GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS dwFields, uint dwRadix, uint dwTimeout, IDebugReference2[] rgpArgs, uint dwArgCount, DEBUG_PROPERTY_INFO[] pPropertyInfo)
        {
            pPropertyInfo[0] = PropertyInfo;
            rgpArgs = null;

            return Constants.S_OK;
        }

        public int GetReference(out IDebugReference2 ppReference)
        {
            throw new NotImplementedException();
        }

        public int GetSize(out uint pdwSize)
        {
            throw new NotImplementedException();
        }

        public int SetValueAsReference(IDebugReference2[] rgpArgs, uint dwArgCount, IDebugReference2 pValue, uint dwTimeout)
        {
            throw new NotImplementedException();
        }

        public int SetValueAsString(string pszValue, uint dwRadix, uint dwTimeout)
        {
            throw new NotImplementedException();
        }
    }
}
