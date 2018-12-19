//#define debug
//#if debug
//#define functiontimeout
//#define pec
//#define frozen
//#define dirty
//#define readback
//#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.ComponentModel;
using O2Micro.Cobra.Communication;
using O2Micro.Cobra.Common;
using System.Windows.Forms;

namespace O2Micro.Cobra.KALL17
{
    internal class DEMBehaviorManage
    {
        private byte calATECRC;
        private byte calUSRCRC;
        //父对象保存
        private DEMDeviceManage m_parent;
        public DEMDeviceManage parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }

        private object m_lock = new object();
        private CCommunicateManager m_Interface = new CCommunicateManager();

        public void Init(object pParent)
        {
            parent = (DEMDeviceManage)pParent;
            CreateInterface();

        }

        #region 端口操作
        public bool CreateInterface()
        {
            bool bdevice = EnumerateInterface();
            if (!bdevice) return false;

            return m_Interface.OpenDevice(ref parent.m_busoption);
        }

        public bool DestroyInterface()
        {
            return m_Interface.CloseDevice();
        }

        public bool EnumerateInterface()
        {
            return m_Interface.FindDevices(ref parent.m_busoption);
        }
        #endregion

        #region 操作寄存器操作
        #region 操作寄存器父级操作
        protected UInt32 ReadWord(byte reg, ref UInt16 pval)
        {
            UInt32 ret = 0;
            lock (m_lock)
            {
                ret = OnReadWord(ElementDefine.OR_RD_CMD, reg, ref pval);
            }
            return ret;
        }

        protected UInt32 WriteWord(byte reg, UInt16 val)
        {
            UInt32 ret = 0;
            lock (m_lock)
            {
                ret = OnWriteWord(ElementDefine.OR_WR_CMD, reg, val);
            }
            return ret;
        }
        #endregion

        #region 操作寄存器子级操作
        protected byte crc8_calc(ref byte[] pdata, UInt16 n)
        {
            byte crc = 0;
            byte crcdata;
            UInt16 i, j;

            for (i = 0; i < n; i++)
            {
                crcdata = pdata[i];
                for (j = 0x80; j != 0; j >>= 1)
                {
                    if ((crc & 0x80) != 0)
                    {
                        crc <<= 1;
                        crc ^= 0x07;
                    }
                    else
                        crc <<= 1;

                    if ((crcdata & j) != 0)
                        crc ^= 0x07;
                }
            }
            return crc;
        }

        protected UInt32 OnReadWord(byte cmd, byte reg, ref UInt16 pval)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            ret = OnSPI_ReadCmd(cmd, reg, ref pval);
            return ret;
        }

        protected UInt32 OnWriteWord(byte cmd, byte reg, UInt16 val)
        {
#if debug
            return 0;
#else
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            ret = OnSPI_WriteCmd(cmd, reg, val);
            return ret;
#endif
        }

        protected UInt32 OnSPI_ReadCmd(byte Cmd_Len, byte reg, ref UInt16 pWval)
        {
#if debug
            pWval = 1;
            return 0;
#else
            int wlen = 0, blen = 0, slen = 0;
            UInt16 DataOutLen = 0;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            blen = (int)(Cmd_Len & 0x0F) + 1;
            wlen = blen * 2;
            slen = ElementDefine.CMD_SECTION_SIZE + wlen; //PEC

            byte[] rdbuf = new byte[slen];
            byte[] wrbuf = new byte[slen];

            wrbuf[0] = Cmd_Len;
            wrbuf[1] = reg;

            for (int i = 2; i < slen; i++) wrbuf[i] = 0;
            for (int k = 0; k < ElementDefine.SPI_RETRY_COUNT; k++)
            {
                if (m_Interface.WriteDevice(wrbuf, ref rdbuf, ref DataOutLen, (ushort)(slen - 2)))
                {
                    ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
                    break;
                }
                ret = LibErrorCode.IDS_ERR_DEM_FUN_TIMEOUT;
                Thread.Sleep(10);
            }

            if (ret == LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                if (rdbuf[slen - 1] != crc8_calc(ref rdbuf, (UInt16)(slen - 1)))
                    ret = LibErrorCode.IDS_ERR_SPI_CRC_CHECK;
                else if (rdbuf[1] != wrbuf[1])
                    ret = LibErrorCode.IDS_ERR_SPI_DATA_MISMATCH;
                else if (rdbuf[0] != wrbuf[0])
                    ret = LibErrorCode.IDS_ERR_SPI_CMD_MISMATCH;
                else
                {
                    for (int i = 0; i < (int)blen; i++)
                        pWval = SharedFormula.MAKEWORD(rdbuf[i * 2 + 3], rdbuf[(i + 1) * 2]);
                }
            }
            return ret;
#endif
        }

        protected UInt32 OnSPI_WriteCmd(byte Cmd_Len, byte reg, UInt16 wval)
        {
#if debug
            return 0;
#else
            UInt16 DataOutLen = 0;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            byte[] rdbuf = new byte[ElementDefine.CMD_SECTION_SIZE + 2];
            byte[] wrbuf = new byte[ElementDefine.CMD_SECTION_SIZE + 2];

            wrbuf[0] = Cmd_Len;				// cmd 
            wrbuf[1] = reg;					// reg
            wrbuf[2] = SharedFormula.HiByte(wval);		//HByte
            wrbuf[3] = SharedFormula.LoByte(wval);		//LByte
            wrbuf[4] = crc8_calc(ref wrbuf, 4);	// pec

            for (int i = 0; i < ElementDefine.SPI_RETRY_COUNT; i++)
            {
                if (m_Interface.WriteDevice(wrbuf, ref rdbuf, ref DataOutLen, ElementDefine.CMD_SECTION_SIZE))
                {
                    ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
                    break;
                }
                ret = LibErrorCode.IDS_ERR_DEM_FUN_TIMEOUT;
                Thread.Sleep(10);
            }

            if (ret == LibErrorCode.IDS_ERR_SUCCESSFUL)
            {
                if (rdbuf[4] != wrbuf[4])
                    ret = LibErrorCode.IDS_ERR_SPI_CRC_CHECK;
                else if (rdbuf[3] != wrbuf[3])
                    ret = LibErrorCode.IDS_ERR_SPI_DATA_MISMATCH;
                else if (rdbuf[2] != wrbuf[2])
                    ret = LibErrorCode.IDS_ERR_SPI_DATA_MISMATCH;
                else if (rdbuf[1] != wrbuf[1])
                    ret = LibErrorCode.IDS_ERR_SPI_DATA_MISMATCH;
                else if (rdbuf[0] != wrbuf[0])
                    ret = LibErrorCode.IDS_ERR_SPI_CMD_MISMATCH;
            }
            return ret;
#endif
        }
        #endregion
        #endregion

        #region EFUSE寄存器操作
        #region EFUSE寄存器父级操作
        internal UInt32 EFUSEReadWord(byte reg, ref UInt16 pval)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            lock (m_lock)
            {
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL) return ret;

                ret = OnEFUSEReadWord(reg, ref pval);
            }
            return ret;
        }

        protected UInt32 EFUSEWriteWord(byte reg, UInt16 val)
        {
            UInt32 ret = 0;
            lock (m_lock)
            {
                ret = OnEFUSEWriteWord(reg, val);
            }
            return ret;
        }
        #endregion

        #region EFUSE寄存器子级操作

        protected UInt32 OnWorkMode(ElementDefine.WORK_MODE wkm)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            ret = OnWriteWord(ElementDefine.OR_WR_CMD, ElementDefine.WORKMODE_OFFSET, (byte)wkm);
            return ret;
        }

        protected UInt32 OnEFUSEReadWord(byte reg, ref UInt16 pval)
        {
            return OnReadWord(ElementDefine.EF_RD_CMD, reg, ref pval);
        }

        protected UInt32 OnEFUSEWriteWord(byte reg, UInt16 val)
        {
            uint ret = OnWriteWord(ElementDefine.EF_WR_CMD, reg, val);
            Thread.Sleep(5);
            return ret;
        }

        #endregion
        #endregion

        #region EFUSE功能操作
        #region EFUSE功能父级操作

        protected UInt32 WorkMode(ElementDefine.WORK_MODE wkm)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            lock (m_lock)
            {
                ret = OnWorkMode(wkm);
            }
            return ret;
        }

        protected UInt32 BlockErase(ref TASKMessage msg)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            return ret;
        }

        #endregion
        #endregion

        #region 基础服务功能设计
        public UInt32 EraseEEPROM(ref TASKMessage msg)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            /*ParamContainer demparameterlist = msg.task_parameterlist;
            if (demparameterlist == null) return ret;

            foreach (Parameter p in demparameterlist.parameterlist)
                p.errorcode = LibErrorCode.IDS_ERR_SUCCESSFUL;

            ret = BlockErase(ref msg);*/
            return ret;
        }

        public UInt32 Read(ref TASKMessage msg)
        {
            Reg reg = null;
            bool bsim = true;
            byte baddress = 0;
            UInt16 wdata = 0;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            List<byte> EFUSEReglist = new List<byte>();
            List<byte> OpReglist = new List<byte>();

            ParamContainer demparameterlist = msg.task_parameterlist;
            if (demparameterlist == null) return ret;

            AutomationElement aElem = parent.m_busoption.GetATMElementbyGuid(AutomationElement.GUIDATMTestStart);
            if (aElem != null)
            {
                bsim |= (aElem.dbValue > 0.0) ? true : false;
                aElem = parent.m_busoption.GetATMElementbyGuid(AutomationElement.GUIDATMTestSimulation);
                bsim |= (aElem.dbValue > 0.0) ? true : false;
            }

            foreach (Parameter p in demparameterlist.parameterlist)
            {
                if ((p.guid & ElementDefine.SectionMask) == ElementDefine.VirtualElement)    //略过虚拟参数
                    continue;
                switch (p.guid & ElementDefine.ElementMask)
                {
                    case ElementDefine.EFUSEElement:
                        {
                            if (p == null) break;
                            if (p.errorcode == LibErrorCode.IDS_ERR_DEM_PARAM_READ_WRITE_UNABLE) continue;
                            foreach (KeyValuePair<string, Reg> dic in p.reglist)
                            {
                                reg = dic.Value;
                                baddress = (byte)reg.address;
                                EFUSEReglist.Add(baddress);
                            }
                            break;
                        }
                    case ElementDefine.OperationElement:
                        {
                            if (p == null) break;
                            foreach (KeyValuePair<string, Reg> dic in p.reglist)
                            {
                                reg = dic.Value;
                                baddress = (byte)reg.address;
                                OpReglist.Add(baddress);
                            }
                            break;
                        }
                    case ElementDefine.TemperatureElement:
                        break;
                }
            }

            EFUSEReglist = EFUSEReglist.Distinct().ToList();
            OpReglist = OpReglist.Distinct().ToList();
            //Read 
            if (EFUSEReglist.Count != 0)
            {
                /*List<byte> EFATEList = new List<byte>();
                List<byte> EFUSRList = new List<byte>();
                foreach (byte addr in EFUSEReglist)
                {
                    if (addr <= 0x26 && addr >= 0x20)
                        EFATEList.Add(addr);
                    else if (addr <= 0x2f && addr >= 0x27)
                        EFUSRList.Add(addr);
                }
                if (EFATEList.Count != 0)
                {
                    ret = CheckATECRC();
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                }

                if (EFUSRList.Count != 0)
                {
                    ret = CheckUSRCRC();
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                }*/
                foreach (byte badd in EFUSEReglist)
                {
                    ret = EFUSEReadWord(badd, ref wdata);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    parent.m_EFRegImg[badd].err = ret;
                    parent.m_EFRegImg[badd].val = wdata;
                }

                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL) return ret;
                /*if (EFUSEReglist.Count != 0)
                {
                    ret = CheckCRC();   //这个函数除了检查CRC，也读到了寄存器的内容
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                }*/
            }

            foreach (byte badd in OpReglist)
            {
                //else
                {
                    ret = ReadWord(badd, ref wdata);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    parent.m_OpRegImg[badd].err = ret;
                    parent.m_OpRegImg[badd].val = wdata;
                }
            }
            return ret;
        }

        private bool isATEFRZ()
        {
            if (parent.isAMTEnabled)
                return true;
            else
                return (parent.m_EFRegImgEX[0x0f].val & 0x8000) == 0x8000;
        }

        private UInt32 CheckCRC()
        {
            //UInt16 len = 8;
            //byte tmp = 0;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            byte[] atebuf = new byte[31];

            ret = ReadATECRCRefReg();   //这边已经读到了寄存器的内容
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;

            if (!isATEFRZ())        //如果没有Freeze，就不需要检查CRC
                return LibErrorCode.IDS_ERR_SUCCESSFUL;

            GetATECRCRef(ref atebuf);
            calATECRC = CalEFUSECRC(atebuf, 31);

            byte readATECRC = 0;
            ret = ReadATECRC(ref readATECRC);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;

            if (readATECRC == calATECRC)
                return LibErrorCode.IDS_ERR_SUCCESSFUL;
            else
            {
                parent.m_EFRegImgEX[0x0f].err = LibErrorCode.IDS_ERR_DEM_ATE_CRC_ERROR;
                return LibErrorCode.IDS_ERR_DEM_ATE_CRC_ERROR;
            }
        }

        private UInt32 ReadATECRC(ref byte crc)
        {
            ushort wdata = 0;
            if (parent.isAMTEnabled)
            {
                AutoMationTest.AutoMationTest.bIsCRCRegister = true;    //Tell AMT we are reading CRC register
                AutoMationTest.AutoMationTest.regCRCInfor.address = 0x8f;
                AutoMationTest.AutoMationTest.regCRCInfor.startbit = 0x00;
                AutoMationTest.AutoMationTest.regCRCInfor.bitsnumber = 8;

                parent.m_EFRegImg[0x8f].val &= 0xff00;
                parent.m_EFRegImg[0x8f].val |= calATECRC;    //Deliver calCRC to AMT
            }
            else
            {
                AutoMationTest.AutoMationTest.bIsCRCRegister = false;
            }
            parent.m_EFRegImg[0x8f].err = ReadWord(0x8f, ref wdata);
            if (parent.m_EFRegImg[0x8f].err != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return parent.m_EFRegImg[0x8f].err;
            parent.m_EFRegImg[0x8f].val = wdata;
            crc = (byte)(wdata & 0x00ff);
            return LibErrorCode.IDS_ERR_SUCCESSFUL;
        }
        private UInt32 ReadATECRCRefReg()
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            for (byte i = 0x80; i <= 0x8f; i++)
            {
                ushort wdata = 0;
                parent.m_EFRegImg[i].err = ReadWord(i, ref wdata);
                //if (parent.m_EFRegImg[i].err != LibErrorCode.IDS_ERR_SUCCESSFUL)
                //return parent.m_EFRegImg[i].err;
                parent.m_EFRegImg[i].val = wdata;
                ret |= parent.m_EFRegImg[i].err;
            }
            return ret;
        }
        private void GetATECRCRef(ref byte[] buf)
        {
            //byte[] dat = new byte[0x0b];
            //byte[] tmp = new byte[27];
            /*for (byte i = 0; i < 20; i++)
            {
                byte shiftdigit = (byte)((i % 4) * 4);
                int reg = i / 4;
                buf[i] = (byte)((parent.m_EFRegImgEX[reg].val & (0x0f << shiftdigit)) >> shiftdigit);
            }
            buf[20] = (byte)((parent.m_EFRegImgEX[5].val & 0x00f0) >> 4);
            buf[21] = (byte)((parent.m_EFRegImgEX[5].val & 0x0f00) >> 8);
            buf[22] = (byte)((parent.m_EFRegImgEX[5].val & 0xf000) >> 12);


            buf[23] = (byte)(parent.m_EFRegImgEX[5].val & 0x000f);
            buf[24] = (byte)((parent.m_EFRegImgEX[5].val & 0x00f0) >> 4);
            buf[25] = (byte)((parent.m_EFRegImgEX[5].val & 0x0f00) >> 8);
            buf[26] = (byte)((parent.m_EFRegImgEX[5].val & 0xf000) >> 12);*/
            for (ushort i = 0; i < 15; i++)
            {
                buf[i * 2] = (byte)(parent.m_EFRegImgEX[i].val & 0x00ff);
                buf[i * 2 + 1] = (byte)((parent.m_EFRegImgEX[i].val & 0xff00) >> 8);
            }
            buf[30] = (byte)((parent.m_EFRegImgEX[0x0f].val & 0xff00) >> 8);

        }

        private byte CalEFUSECRC(byte[] buf, UInt16 len)
        {
            return crc8_calc(buf, len);
        }

        protected byte crc8_calc(byte[] pdata, UInt16 n)
        {
            byte crc = 0;
            byte crcdata;
            UInt16 i, j;

            for (i = 0; i < n; i++)
            {
                crcdata = pdata[i];
                for (j = 0x80; j != 0; j >>= 1)
                {
                    if ((crc & 0x80) != 0)
                    {
                        crc <<= 1;
                        crc ^= 0x07;
                    }
                    else
                        crc <<= 1;

                    if ((crcdata & j) != 0)
                        crc ^= 0x07;
                }
            }
            return crc;
        }

        private UInt32 UnLockCfgArea()
        {
            ushort tmp = 0;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            ret = ReadWord(0x56, ref tmp);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;
            if ((tmp & 0x0001) == 0x0001)
                return ret;
            ret = WriteWord(0x56, 0x7717);
            return ret;
        }

        public UInt32 Write(ref TASKMessage msg)    //因为Efuse是在Expert页面写，所以没有复杂逻辑
        {
            Reg reg = null;
            byte baddress = 0;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            List<byte> EFUSEReglist = new List<byte>();
            UInt16[] EFUSEATEbuf = new UInt16[16];
            List<byte> OpReglist = new List<byte>();
            UInt16[] pdata = new UInt16[6];

            ParamContainer demparameterlist = msg.task_parameterlist;
            if (demparameterlist == null) return ret;
            if (msg.gm.sflname == "OPConfig")
            {
                ret = UnLockCfgArea();
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    return ret;
            }
            foreach (Parameter p in demparameterlist.parameterlist)
            {
                if ((p.guid & ElementDefine.SectionMask) == ElementDefine.VirtualElement)    //略过虚拟参数
                    continue;
                switch (p.guid & ElementDefine.ElementMask)
                {
                    case ElementDefine.EFUSEElement:
                        {
                            if (p == null) break;
                            if ((p.errorcode == LibErrorCode.IDS_ERR_DEM_PARAM_READ_WRITE_UNABLE) || (p.errorcode == LibErrorCode.IDS_ERR_DEM_PARAM_WRITE_UNABLE)) continue;
                            foreach (KeyValuePair<string, Reg> dic in p.reglist)
                            {
                                reg = dic.Value;
                                baddress = (byte)reg.address;
                                EFUSEReglist.Add(baddress);
                            }
                            break;
                        }
                    case ElementDefine.OperationElement:
                        {
                            if (p == null) break;
                            foreach (KeyValuePair<string, Reg> dic in p.reglist)
                            {
                                reg = dic.Value;
                                baddress = (byte)reg.address;
                                OpReglist.Add(baddress);
                            }
                            break;
                        }
                    case ElementDefine.TemperatureElement:
                        break;
                }
            }

            EFUSEReglist = EFUSEReglist.Distinct().ToList();
            OpReglist = OpReglist.Distinct().ToList();

            foreach (byte badd in EFUSEReglist)
            {
                ret = OnEFUSEWriteWord(badd, parent.m_EFRegImg[badd].val);
                parent.m_EFRegImg[badd].err = ret;
            }

            foreach (byte badd in OpReglist)
            {
                ret = WriteWord(badd, parent.m_OpRegImg[badd].val);
                parent.m_OpRegImg[badd].err = ret;
            }

            return ret;
        }

        public UInt32 BitOperation(ref TASKMessage msg)
        {
            Reg reg = null;
            byte baddress = 0;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            List<byte> OpReglist = new List<byte>();

            ParamContainer demparameterlist = msg.task_parameterlist;
            if (demparameterlist == null) return ret;

            foreach (Parameter p in demparameterlist.parameterlist)
            {
                if ((p.guid & ElementDefine.SectionMask) == ElementDefine.VirtualElement)    //略过虚拟参数
                    continue;
                switch (p.guid & ElementDefine.ElementMask)
                {
                    case ElementDefine.OperationElement:
                        {
                            if (p == null) break;
                            foreach (KeyValuePair<string, Reg> dic in p.reglist)
                            {
                                reg = dic.Value;
                                baddress = (byte)reg.address;

                                parent.m_OpRegImg[baddress].val = 0x00;
                                parent.WriteToRegImg(p, 1);
                                OpReglist.Add(baddress);

                            }
                            break;
                        }
                }
            }

            OpReglist = OpReglist.Distinct().ToList();

            //Write 
            foreach (byte badd in OpReglist)
            {
                ret = WriteWord(badd, parent.m_OpRegImg[badd].val);
                parent.m_OpRegImg[badd].err = ret;
            }

            return ret;
        }

        public UInt32 ConvertHexToPhysical(ref TASKMessage msg)
        {
            Parameter param = null;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            List<Parameter> EFUSEParamList = new List<Parameter>();
            List<Parameter> OpParamList = new List<Parameter>();

            ParamContainer demparameterlist = msg.task_parameterlist;
            if (demparameterlist == null) return ret;

            foreach (Parameter p in demparameterlist.parameterlist)
            {
                if ((p.guid & ElementDefine.SectionMask) == ElementDefine.VirtualElement)    //略过虚拟参数
                    continue;
                switch (p.guid & ElementDefine.ElementMask)
                {
                    case ElementDefine.EFUSEElement:
                        {
                            if (p == null) break;
                            EFUSEParamList.Add(p);
                            break;
                        }
                    case ElementDefine.OperationElement:
                        {
                            if (p == null) break;
                            OpParamList.Add(p);
                            break;
                        }
                    case ElementDefine.TemperatureElement:
                        {
                            param = p;
                            m_parent.Hex2Physical(ref param);
                            break;
                        }
                }
            }

            if (EFUSEParamList.Count != 0)
            {
                for (int i = 0; i < EFUSEParamList.Count; i++)
                {
                    param = (Parameter)EFUSEParamList[i];
                    if (param == null) continue;
                    if ((param.guid & ElementDefine.ElementMask) == ElementDefine.TemperatureElement) continue;

                    m_parent.Hex2Physical(ref param);
                }
            }

            if (OpParamList.Count != 0)
            {
                for (int i = 0; i < OpParamList.Count; i++)
                {
                    param = (Parameter)OpParamList[i];
                    if (param == null) continue;
                    if ((param.guid & ElementDefine.ElementMask) == ElementDefine.TemperatureElement) continue;

                    m_parent.Hex2Physical(ref param);
                }
            }

            return ret;
        }

        public UInt32 ConvertPhysicalToHex(ref TASKMessage msg)
        {
            Parameter param = null;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            List<Parameter> EFUSEParamList = new List<Parameter>();
            List<Parameter> OpParamList = new List<Parameter>();

            ParamContainer demparameterlist = msg.task_parameterlist;
            if (demparameterlist == null) return ret;

            foreach (Parameter p in demparameterlist.parameterlist)
            {
                if ((p.guid & ElementDefine.SectionMask) == ElementDefine.VirtualElement)    //略过虚拟参数
                    continue;
                switch (p.guid & ElementDefine.ElementMask)
                {
                    case ElementDefine.EFUSEElement:
                        {
                            if (p == null) break;
                            EFUSEParamList.Add(p);
                            break;
                        }
                    case ElementDefine.OperationElement:
                        {
                            if (p == null) break;
                            OpParamList.Add(p);
                            break;
                        }
                    case ElementDefine.TemperatureElement:
                        {
                            param = p;
                            m_parent.Physical2Hex(ref param);
                            break;
                        }
                }
            }

            if (EFUSEParamList.Count != 0)
            {
                for (int i = 0; i < EFUSEParamList.Count; i++)
                {
                    param = (Parameter)EFUSEParamList[i];
                    if (param == null) continue;
                    if ((param.guid & ElementDefine.ElementMask) == ElementDefine.TemperatureElement) continue;

                    m_parent.Physical2Hex(ref param);
                }
            }

            if (OpParamList.Count != 0)
            {
                for (int i = 0; i < OpParamList.Count; i++)
                {
                    param = (Parameter)OpParamList[i];
                    if (param == null) continue;
                    if ((param.guid & ElementDefine.ElementMask) == ElementDefine.TemperatureElement) continue;

                    m_parent.Physical2Hex(ref param);
                }
            }

            return ret;
        }
        #region SAR
        private uint ReadAvrage(ref TASKMessage msg)
        {
            uint ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            List<double[]> llt = new List<double[]>();
            List<double> avr = new List<double>();
            foreach (Parameter param in msg.task_parameterlist.parameterlist)
            {
                llt.Add(new double[5]);
                avr.Add(0);
            }
            for (int i = 0; i < 5; i++)
            {
                ret = ClearSarTriggerScanFlag();
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    return ret;
                ///
                ret = ChangeSarScanMode(ElementDefine.SAR_MODE.TRIGGER_8);
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    return ret;
                ///
                ret = WaitForSarScanComplete();
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    return ret;
                ret = Read(ref msg);
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    return ret;
                }
                ret = ConvertHexToPhysical(ref msg);
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                {
                    return ret;
                }
                for (int j = 0; j < msg.task_parameterlist.parameterlist.Count; j++)
                {
                    llt[j][i] = msg.task_parameterlist.parameterlist[j].phydata;
                    avr[j] += llt[j][i];
                }
                Thread.Sleep(100);
            }

            for (int j = 0; j < msg.task_parameterlist.parameterlist.Count; j++)
            {
                //llt[j][i] = msg.task_parameterlist.parameterlist[j].phydata;
                avr[j] /= 5;
                int minIndex = 0;
                double err = 999;
                for (int i = 0; i < 5; i++)
                {
                    if (err > Math.Abs(llt[j][i] - avr[j]))
                    {
                        err = Math.Abs(llt[j][i] - avr[j]);
                        minIndex = i;
                    }
                }
                msg.task_parameterlist.parameterlist[j].phydata = llt[j][minIndex];
            }
            return ret;
        }
        UInt32 ChangeSarScanMode(ElementDefine.SAR_MODE scanmode)
        {
            ushort wdata = 0;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            switch (scanmode)
            {
                case ElementDefine.SAR_MODE.AUTO_1:
                    ret = ReadWord(0x58, ref wdata);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    wdata &= 0xfff8;
                    wdata |= 0x0005;
                    ret = WriteWord(0x58, wdata);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;
                case ElementDefine.SAR_MODE.AUTO_8:
                    ret = ReadWord(0x58, ref wdata);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    wdata &= 0xfff8;
                    wdata |= 0x0007;
                    ret = WriteWord(0x58, wdata);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;
                case ElementDefine.SAR_MODE.TRIGGER_1:
                    ret = ReadWord(0x5f, ref wdata);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    wdata &= 0xff20;
                    wdata |= 0x009f;
                    ret = WriteWord(0x5f, wdata);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;
                case ElementDefine.SAR_MODE.TRIGGER_8:
                    ret = ReadWord(0x5f, ref wdata);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    wdata &= 0xff20;
                    wdata |= 0x00df;
                    ret = WriteWord(0x5f, wdata);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;
                case ElementDefine.SAR_MODE.TRIGGER_8_TIME_CURRENT_SCAN:
                    ret = ReadWord(0x5f, ref wdata);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    wdata &= 0xff20;
                    wdata |= 0x00d2;
                    ret = WriteWord(0x5f, wdata);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;
            }
            return ret;
        }

        UInt32 ClearSarTriggerScanFlag()
        {
            return WriteWord(0x5e, 0x0020);
        }

        UInt32 WaitForSarScanComplete()
        {
#if debug
            return LibErrorCode.IDS_ERR_SUCCESSFUL;
#else
            ushort wdata = 0;
            ushort retry_count = 200;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            while ((wdata & 0x0020) != 0x0020)
            {
                retry_count--;
                if (retry_count == 0)
                    return ElementDefine.IDS_ERR_DEM_WAIT_TRIGGER_FLAG_TIMEOUT;
                ret = ReadWord(0x5e, ref wdata);
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    return ret;
            }
            return ret;
#endif
        }

        void GetExtTemp()
        {
            for (byte i = 0; i < 5; i++)
            {
                parent.thms[i].ADC2 = parent.m_OpRegImg[0x13 + i].val;  //120uA时的电压值

                if (parent.thms[i].ADC2 <= 32700)   //120uA档是正确值
                {
                    parent.m_OpRegImg[0x13 + i].val = parent.thms[i].ADC2;
                    parent.thms[i].thm_crrt = 120;
                }
                else    //20uA档是正确值
                {
                    parent.m_OpRegImg[0x13 + i].val = parent.thms[i].ADC1;
                    parent.thms[i].thm_crrt = 20;
                }

                parent.m_OpRegImg[0x13 + i].err = LibErrorCode.IDS_ERR_SUCCESSFUL;
            }
        }
        private UInt32 ReadSAR(ref TASKMessage msg, ElementDefine.SAR_MODE scanmode)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            if (scanmode == ElementDefine.SAR_MODE.DISABLE)
                return ret;

            TASKMessage sarmsg = new TASKMessage();  //only contains sar adc parameters
            TASKMessage tmpmsg = new TASKMessage();  //only contains temperature parameters

            foreach (Parameter p in msg.task_parameterlist.parameterlist)
            {
                if (p.guid != ElementDefine.BASIC_CADC && p.guid != ElementDefine.TRIGGER_CADC && p.guid != ElementDefine.MOVING_CADC)
                    sarmsg.task_parameterlist.parameterlist.Add(p);
            }

            ushort thm_crrt_sel = 0;
            ret = ReadWord(0x5a, ref thm_crrt_sel); //保存原始值
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;

            ret = WriteWord(0x5a, 0x0001);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;
            ret = ClearSarTriggerScanFlag();
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;
            ret = ChangeSarScanMode(scanmode);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;
            if (scanmode == ElementDefine.SAR_MODE.TRIGGER_1 || scanmode == ElementDefine.SAR_MODE.TRIGGER_8)
            {
                ret = WaitForSarScanComplete();
                if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                    return ret;
            }
            else
            {
                Thread.Sleep(40);
            }
            ret = Read(ref sarmsg);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;
            
            //Issue 1169
            ushort tmp = 0;
            ret = ReadWord(0x52, ref tmp); 
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;
            if ((tmp & 0x1000) == 0x1000)   //read Vwkup
            //if(true)
            {
                parent.m_Vwkup.val = parent.m_OpRegImg[0x1a].val;
                parent.m_OpRegImg[0x1a].val = ElementDefine.PARAM_HEX_ERROR;
            }
            else                            //read Vpack
            {
                parent.m_Vwkup.val = ElementDefine.PARAM_HEX_ERROR;
            }

            for (byte i = 0; i < 5; i++)
            {
                parent.thms[i].ADC1 = parent.m_OpRegImg[0x13 + i].val;  //20uA时的电压值
            }


            ret = WriteWord(0x5a, 0x0002);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;


            ret = ClearSarTriggerScanFlag();
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;
            ret = ChangeSarScanMode(ElementDefine.SAR_MODE.TRIGGER_1);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;
            ret = WaitForSarScanComplete();
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;

            foreach (Parameter p in sarmsg.task_parameterlist.parameterlist)
            {
                if (p.subtype == (ushort)ElementDefine.SUBTYPE.EXT_TEMP)
                    tmpmsg.task_parameterlist.parameterlist.Add(p);
            }
            ret = Read(ref tmpmsg);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;

            GetExtTemp();

            ret = WriteWord(0x5a, thm_crrt_sel);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;
            return ret;
        }
        #endregion

        private UInt32 ReadCADC(ElementDefine.CADC_MODE mode)       //MP version new method. Do 4 time average by HW, and we can also have the trigger flag and coulomb counter work at the same time.
        {
            parent.cadc_mode = mode;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            ushort temp = 0;
            switch (mode)
            {
                case ElementDefine.CADC_MODE.DISABLE:
                    #region disable
                    ret = WriteWord(0x30, 0x00);        //clear all
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    #endregion
                    break;
                case ElementDefine.CADC_MODE.MOVING:
                    #region moving mode
                    ret = ActiveModeCheck();
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    bool cadc_moving_flag = false;
                    {
                        ret = WriteWord(0x61, 0x0004);        //Clear cadc_moving_flag
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return ret;
                        ret = WriteWord(0x30, 0x18);        //Set cc_always_enable, moving_average_enable, sw_cadc_ctrl=0b00
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return ret;
                        for (byte i = 0; i < ElementDefine.CADC_RETRY_COUNT; i++)
                        {
                            Thread.Sleep(20);
                            ret = ReadWord(0x61, ref temp);
                            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                                return ret;
#if debug
                    cadc_moving_flag = true;
                    break;
#else
                            if ((temp & 0x0004) == 0x0004)
                            {
                                cadc_moving_flag = true;
                                break;
                            }
#endif
                        }
                        if (cadc_moving_flag)   //转换完成
                        {
#if debug
                    temp = 15;
#else
                            ret = ReadWord(0x39, ref temp);
#endif
                            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                                return ret;
                        }
                        else
                        {
                            ret = ElementDefine.IDS_ERR_DEM_READCADC_TIMEOUT;
                        }
                    }
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;

                    parent.m_OpRegImg[0x39].err = ret;
                    parent.m_OpRegImg[0x39].val = temp;
                    #endregion
                    break;
                case ElementDefine.CADC_MODE.TRIGGER:
                    #region trigger mode
                    ret = ActiveModeCheck();
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    bool cadc_trigger_flag = false;
                    {
                        ret = WriteWord(0x5e, 0x8000);        //Clear cadc_trigger_flag
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return ret;
                        ret = WriteWord(0x30, 0x06);        //Set cadc_one_or_four, sw_cadc_ctrl=0b10
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return ret;
                        for (byte i = 0; i < ElementDefine.CADC_RETRY_COUNT; i++)
                        {
                            Thread.Sleep(20);
                            ret = ReadWord(0x5e, ref temp);
                            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                                return ret;
#if debug
                            cadc_trigger_flag = true;
                    break;
#else
                            if ((temp & 0x8000) == 0x8000)
                            {
                                cadc_trigger_flag = true;
                                break;
                            }
#endif
                        }
                        if (cadc_trigger_flag)   //转换完成
                        {
#if debug
                    temp = 15;
#else
                            ret = ReadWord(0x38, ref temp);
#endif
                            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                                return ret;
                        }
                        else
                        {
                            ret = ElementDefine.IDS_ERR_DEM_READCADC_TIMEOUT;
                        }
                    }
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;

                    parent.m_OpRegImg[0x38].err = ret;
                    parent.m_OpRegImg[0x38].val = temp;
                    #endregion
                    break;
            }

            return ret;
        }               //trigger mode with 4 time average
        private void TRIGGERCADCHex2Physical(ref Parameter CADC)
        {
            short s = (short)parent.m_OpRegImg[0x38].val;
            CADC.phydata = s * CADC.phyref * 1000 / parent.etrx; //需要带符号
        }
        private UInt32 ActiveModeCheck()
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            ushort tmp = 0;
            ret = ReadWord(0x57, ref tmp);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;
            if ((tmp & 0x0080) != 0x0080)
            {
                ret = ElementDefine.IDS_ERR_DEM_ACTIVE_MODE_ERROR;
            }
            return ret;
        }
        public UInt32 Command(ref TASKMessage msg)
        {
            TASKMessage MSG = new TASKMessage(); Parameter Current = new Parameter(); double AverageHex = 0; ushort wdata = 0;


            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            TASKMessage tmpmsg = new TASKMessage();  //only contains temperature parameters

            ushort tmp = 0;
            switch ((ElementDefine.COMMAND)msg.sub_task)
            {
                #region Scan SFL commands
                case ElementDefine.COMMAND.OPTIONS:
                    var options = SharedAPI.DeserializeStringToDictionary<string,string>(msg.sub_task_json);
                    switch (options["SAR ADC Mode"])
                    {
                        case "Disable":
                            ret = ReadSAR(ref msg, ElementDefine.SAR_MODE.DISABLE);
                            break;
                        case "1_Time":
                            ret = ReadSAR(ref msg, ElementDefine.SAR_MODE.TRIGGER_1);
                            break;
                        case "8_Time_Average":
                            ret = ReadSAR(ref msg, ElementDefine.SAR_MODE.TRIGGER_8);
                            break;
                        case "Auto_1":
                            ret = ReadSAR(ref msg, ElementDefine.SAR_MODE.AUTO_1);
                            break;
                        case "Auto_8":
                            ret = ReadSAR(ref msg, ElementDefine.SAR_MODE.AUTO_8);
                            break;
                    }
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    switch (options["CADC Mode"])
                    {
                        case "Disable":
                            ret = ReadCADC(ElementDefine.CADC_MODE.DISABLE);
                            break;
                        case "Trigger":
                            ret = ReadCADC(ElementDefine.CADC_MODE.TRIGGER);
                            break;
                        case "Moving":
                            ret = ReadCADC(ElementDefine.CADC_MODE.MOVING);
                            break;
                    }
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                /*case ElementDefine.COMMAND.SAR_TRIGGER_1_CADC_DISABLE:     //Trigger 1 time Scan
                    ret = ReadCADC(ElementDefine.CADC_MODE.DISABLE);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = ReadSAR(ref msg, ElementDefine.SAR_MODE.TRIGGER_1);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;
                case ElementDefine.COMMAND.SAR_TRIGGER_8_CADC_DISABLE:     //Trigger 8 time Scan
                    ret = ReadCADC(ElementDefine.CADC_MODE.DISABLE);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = ReadSAR(ref msg, ElementDefine.SAR_MODE.TRIGGER_8);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;
                case ElementDefine.COMMAND.SAR_AUTO_1_CADC_DISABLE:     //Auto 1 time Scan
                    ret = ReadCADC(ElementDefine.CADC_MODE.DISABLE);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = ReadSAR(ref msg, ElementDefine.SAR_MODE.AUTO_1);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;
                case ElementDefine.COMMAND.SAR_AUTO_8_CADC_DISABLE:     //Auto 8 time Scan
                    ret = ReadCADC(ElementDefine.CADC_MODE.DISABLE);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = ReadSAR(ref msg, ElementDefine.SAR_MODE.AUTO_8);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;


                case ElementDefine.COMMAND.SAR_TRIGGER_1_CADC_TRIGGER:     //Trigger 1 time Scan
                    ret = ReadCADC(ElementDefine.CADC_MODE.TRIGGER);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = ReadSAR(ref msg, ElementDefine.SAR_MODE.TRIGGER_1);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;
                case ElementDefine.COMMAND.SAR_TRIGGER_8_CADC_TRIGGER:     //Trigger 8 time Scan
                    ret = ReadCADC(ElementDefine.CADC_MODE.TRIGGER);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = ReadSAR(ref msg, ElementDefine.SAR_MODE.TRIGGER_8);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;
                case ElementDefine.COMMAND.SAR_AUTO_1_CADC_TRIGGER:     //Auto 1 time Scan
                    ret = ReadCADC(ElementDefine.CADC_MODE.TRIGGER);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = ReadSAR(ref msg, ElementDefine.SAR_MODE.AUTO_1);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;
                case ElementDefine.COMMAND.SAR_AUTO_8_CADC_TRIGGER:     //Auto 8 time Scan
                    ret = ReadCADC(ElementDefine.CADC_MODE.TRIGGER);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = ReadSAR(ref msg, ElementDefine.SAR_MODE.AUTO_8);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;


                case ElementDefine.COMMAND.SAR_TRIGGER_1_CADC_MOVING:     //Trigger 1 time Scan
                    ret = ReadCADC(ElementDefine.CADC_MODE.MOVING);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = ReadSAR(ref msg, ElementDefine.SAR_MODE.TRIGGER_1);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;
                case ElementDefine.COMMAND.SAR_TRIGGER_8_CADC_MOVING:     //Trigger 8 time Scan
                    ret = ReadCADC(ElementDefine.CADC_MODE.MOVING);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = ReadSAR(ref msg, ElementDefine.SAR_MODE.TRIGGER_8);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;
                case ElementDefine.COMMAND.SAR_AUTO_1_CADC_MOVING:     //Auto 1 time Scan
                    ret = ReadCADC(ElementDefine.CADC_MODE.MOVING);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = ReadSAR(ref msg, ElementDefine.SAR_MODE.AUTO_1);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;
                case ElementDefine.COMMAND.SAR_AUTO_8_CADC_MOVING:     //Auto 8 time Scan
                    ret = ReadCADC(ElementDefine.CADC_MODE.MOVING);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = ReadSAR(ref msg, ElementDefine.SAR_MODE.AUTO_8);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;
                case ElementDefine.COMMAND.CADC_ONLY:     //Auto 8 time Scan
                    ret = ReadCADC(ElementDefine.CADC_MODE.MOVING);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;*/
                    break;
                #endregion
                #region trim
                case ElementDefine.COMMAND.SLOP_TRIM:
                    Parameter param = null;
                    Parameter CellCurrent = null;
                    Parameter TRIGGER_CADC = null;
                    ParamContainer demparameterlist = msg.task_parameterlist;
                    if (demparameterlist == null) return ret;


                    Parameter CellNum = parent.m_Section_ParamlistContainer.GetParameterListByGuid(ElementDefine.OperationElement).GetParameterByGuid(ElementDefine.CellNum);
                    TASKMessage tmp_msg = new TASKMessage();
                    ParamContainer pc = new ParamContainer();
                    pc.parameterlist.Add(CellNum);
                    tmp_msg.task_parameterlist = pc;
                    Read(ref tmp_msg);
                    ConvertHexToPhysical(ref tmp_msg);
                    //step1: write 0x8000 to Reg0x70
                    ushort buf = 0;
                    ReadWord(0x70, ref buf);
                    buf |= 0x8000;
                    WriteWord(0x70, buf);
                    //step2: write 0x8001 to Reg0x70
                    ReadWord(0x70, ref buf);
                    buf &= 0xfffc;
                    buf |= 0x8001;
                    WriteWord(0x70, buf);
                    //step3: clear offset
                    for (byte i = 0x98; i <= 0x9F; i++)
                    {
                        if (i == 0x9E)
                        {
                            ReadWord(0x9E, ref buf);
                            //buf &= 0xfe1f;
                            buf &= 0x03e0;
                            WriteWord(0x9E, buf);
                        }
                        else
                            WriteWord(i, (ushort)0);
                    }
                    WriteWord(0x91, (ushort)0);//clear CADC offset and slope

                    for (ushort i = 0; i < demparameterlist.parameterlist.Count; i++)
                    {
                        param = demparameterlist.parameterlist[i];
                        param.sphydata = String.Empty;
                        if (param.guid == ElementDefine.CellCurrent)
                            CellCurrent = param;
                        if (param.guid == ElementDefine.TRIGGER_CADC)
                            TRIGGER_CADC = param;
                    }
                    TASKMessage MSGwithoutCADC = new TASKMessage();
                    MSGwithoutCADC.task_parameterlist = new ParamContainer();
                    MSGwithoutCADC.task_parameterlist.parameterlist = new AsyncObservableCollection<Parameter>();
                    foreach (var p in msg.task_parameterlist.parameterlist)
                    {
                        if (p.guid == TRIGGER_CADC.guid)
                            continue;
                        else
                            MSGwithoutCADC.task_parameterlist.parameterlist.Add(p);
                    }

                    /*TASKMessage CADCmsg = new TASKMessage();
                    CADCmsg.task_parameterlist = new ParamContainer();
                    CADCmsg.task_parameterlist.parameterlist = new AsyncObservableCollection<Parameter>();
                    CADCmsg.task_parameterlist.parameterlist.Add(CADC);*/

                    for (ushort code = 0; code < 16; code++)
                    {
                        //Write Slope Trim
                        WriteWord(0x92, (ushort)((code << 12)));
                        WriteWord(0x93, (ushort)((code << 12) | (code << 8) | (code << 4) | code));
                        WriteWord(0x94, (ushort)((code << 12) | (code << 8) | (code << 4) | code));
                        WriteWord(0x95, (ushort)((code << 12) | (code << 8) | (code << 4) | code));
                        WriteWord(0x96, (ushort)((code << 12) | (code << 8) | (code << 4) | code));
                        buf = 0;
                        ReadWord(0x97, ref buf);
                        buf &= 0x0fe0;
                        WriteWord(0x97, (ushort)(buf | (code << 12) | code));
                        WriteWord(0x91, code);

                        Thread.Sleep(100);

                        ret = ReadAvrage(ref MSGwithoutCADC);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL) return ret;

                        ret = ReadCADC(ElementDefine.CADC_MODE.TRIGGER);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL) return ret;
                        TRIGGERCADCHex2Physical(ref TRIGGER_CADC);

                        for (ushort i = 0; i < demparameterlist.parameterlist.Count; i++)
                        {
                            param = demparameterlist.parameterlist[i];
                            param.sphydata += param.phydata.ToString() + ",";
                        }
                    }

                    TASKMessage currentmsg = new TASKMessage();
                    currentmsg.task_parameterlist = new ParamContainer();
                    currentmsg.task_parameterlist.parameterlist = new AsyncObservableCollection<Parameter>();
                    currentmsg.task_parameterlist.parameterlist.Add(CellCurrent);

                    for (ushort code = 16; code < 32; code++)
                    {
                        buf = 0;
                        ReadWord(0x97, ref buf);
                        buf &= 0xffe0;
                        WriteWord(0x97, (ushort)(buf | code));

                        Thread.Sleep(100);
                        ret = ReadAvrage(ref currentmsg);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL) return ret;

                        CellCurrent.sphydata += CellCurrent.phydata.ToString() + ",";
                    }

                    for (ushort code = 16; code < 256; code++)
                    {
                        WriteWord(0x91, code);

                        Thread.Sleep(100);
                        ret = ReadCADC(ElementDefine.CADC_MODE.TRIGGER);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL) return ret;
                        TRIGGERCADCHex2Physical(ref TRIGGER_CADC);

                        TRIGGER_CADC.sphydata += TRIGGER_CADC.phydata.ToString() + ",";
                    }
                    break;
                #endregion
                #region Action buttons
                case ElementDefine.COMMAND.STANDBY_MODE:
                    ret = WriteWord(0x57, 0x7717);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL) return ret;

                    ret = WriteWord(0x57, 0x0003);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL) return ret;

                    break;
                case ElementDefine.COMMAND.ACTIVE_MODE:
                    ret = WriteWord(0x57, 0x7717);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = WriteWord(0x57, 0x0005);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;
                case ElementDefine.COMMAND.SHUTDOWN_MODE:
                    ret = WriteWord(0x57, 0x7717);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = WriteWord(0x57, 0x000a);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;
                case ElementDefine.COMMAND.CFET_ON:
                    tmp = 0;
                    ret = ActiveModeCheck();
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = ReadWord(0x59, ref tmp);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    tmp |= 0x0002;
                    ret = WriteWord(0x59, tmp);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = ReadWord(0x5b, ref tmp);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    if ((tmp & 0x0200) != 0x0200)
                    {
                        ret = ElementDefine.IDS_ERR_DEM_CFET_ON_FAILED;
                        return ret;
                    }
                    break;
                case ElementDefine.COMMAND.DFET_ON:
                    tmp = 0;
                    ret = ActiveModeCheck();
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = ReadWord(0x59, ref tmp);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    tmp |= 0x0001;
                    ret = WriteWord(0x59, tmp);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = ReadWord(0x5b, ref tmp);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    if ((tmp & 0x0100) != 0x0100)
                    {
                        ret = ElementDefine.IDS_ERR_DEM_DFET_ON_FAILED;
                        return ret;
                    }
                    break;
                case ElementDefine.COMMAND.CFET_OFF:
                    tmp = 0;
                    ret = ActiveModeCheck();
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = ReadWord(0x59, ref tmp);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    tmp &= 0xfffd;
                    ret = WriteWord(0x59, tmp);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = ReadWord(0x5b, ref tmp);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    if ((tmp & 0x0200) != 0x0000)
                    {
                        ret = ElementDefine.IDS_ERR_DEM_CFET_OFF_FAILED;
                        return ret;
                    }
                    break;
                case ElementDefine.COMMAND.DFET_OFF:
                    tmp = 0;
                    ret = ActiveModeCheck();
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = ReadWord(0x59, ref tmp);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    tmp &= 0xfffe;
                    ret = WriteWord(0x59, tmp);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = ReadWord(0x5b, ref tmp);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    if ((tmp & 0x0100) != 0x0000)
                    {
                        ret = ElementDefine.IDS_ERR_DEM_DFET_OFF_FAILED;
                        return ret;
                    }
                    break;
                case ElementDefine.COMMAND.ATE_CRC_CHECK:
                    ret = CheckCRC();
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    break;
                #endregion
                #region CurrentScan
                case ElementDefine.COMMAND.TRIGGER_8_CURRENT_4:


                    foreach (var p in msg.task_parameterlist.parameterlist)
                    {
                        if ((ElementDefine.SUBTYPE)p.subtype == ElementDefine.SUBTYPE.CURRENT)
                        {
                            MSG.task_parameterlist.parameterlist.Add(p);
                            Current = p;
                            break;
                        }
                    }
                    if (Current == null)
                        break;

                    for (int i = 1; i < 5; i++)
                    {
                        FolderMap.WriteFile("\r\n第" + i.ToString() + "次读取");
                        ret = ClearSarTriggerScanFlag();
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return ret;
                        ret = ChangeSarScanMode(ElementDefine.SAR_MODE.TRIGGER_8_TIME_CURRENT_SCAN);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return ret;
                        ret = WaitForSarScanComplete();
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return ret;
                        //ret = Read(ref MSG);

                        ret = ReadWord(0x12, ref wdata);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return ret;

                        parent.m_OpRegImg[0x12].err = ret;
                        parent.m_OpRegImg[0x12].val = wdata;

                        parent.ReadFromRegImg(Current, ref wdata);

                        AverageHex += (short)Current.hexdata;
                        FolderMap.WriteFile("Reg" + Current.reglist["Low"].address.ToString("X2") + " Hex Value is " + ((short)(Current.hexdata)).ToString());
                    }
                    AverageHex /= 4;

                    FolderMap.WriteFile("\r\n\t\tReg" + Current.reglist["Low"].address.ToString("X2") + " Average Hex Value is " + AverageHex.ToString());
                    decimal dtemp = Math.Round((decimal)AverageHex, 0, MidpointRounding.AwayFromZero);
                    short stemp = Convert.ToInt16(dtemp);
                    Current.hexdata = (ushort)stemp;

                    FolderMap.WriteFile("\t\tReg" + Current.reglist["Low"].address.ToString("X2") + " Average Hex Rounding Value is " + ((short)(Current.hexdata)).ToString());

                    parent.WriteToRegImg(Current, Current.hexdata);

                    ret = ReadCADC(ElementDefine.CADC_MODE.MOVING);
                    break;
                case ElementDefine.COMMAND.TRIGGER_8_CURRENT_8:


                    foreach (var p in msg.task_parameterlist.parameterlist)
                    {
                        if ((ElementDefine.SUBTYPE)p.subtype == ElementDefine.SUBTYPE.CURRENT)
                        {
                            MSG.task_parameterlist.parameterlist.Add(p);
                            Current = p;
                            break;
                        }
                    }
                    if (Current == null)
                        break;

                    for (int i = 1; i < 9; i++)
                    {
                        FolderMap.WriteFile("\r\n第" + i.ToString() + "次读取");
                        ret = ClearSarTriggerScanFlag();
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return ret;
                        ret = ChangeSarScanMode(ElementDefine.SAR_MODE.TRIGGER_8_TIME_CURRENT_SCAN);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return ret;
                        ret = WaitForSarScanComplete();
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return ret;
                        //ret = Read(ref MSG);

                        ret = ReadWord(0x12, ref wdata);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            return ret;

                        parent.m_OpRegImg[0x12].err = ret;
                        parent.m_OpRegImg[0x12].val = wdata;

                        parent.ReadFromRegImg(Current, ref wdata);

                        AverageHex += (short)Current.hexdata;
                        FolderMap.WriteFile("Reg" + Current.reglist["Low"].address.ToString("X2") + " Hex Value is " + ((short)(Current.hexdata)).ToString());
                    }
                    AverageHex /= 8;

                    FolderMap.WriteFile("\r\n\t\tReg" + Current.reglist["Low"].address.ToString("X2") + " Average Hex Value is " + AverageHex.ToString());
                    dtemp = Math.Round((decimal)AverageHex, 0, MidpointRounding.AwayFromZero);
                    stemp = Convert.ToInt16(dtemp);
                    Current.hexdata = (ushort)stemp;

                    FolderMap.WriteFile("\t\tReg" + Current.reglist["Low"].address.ToString("X2") + " Average Hex Rounding Value is " + ((short)(Current.hexdata)).ToString());

                    parent.WriteToRegImg(Current, Current.hexdata);

                    ret = ReadCADC(ElementDefine.CADC_MODE.MOVING);
                    break;
                case ElementDefine.COMMAND.TRIGGER_8_CURRENT_1:

                    ret = ClearSarTriggerScanFlag();
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = ChangeSarScanMode(ElementDefine.SAR_MODE.TRIGGER_8_TIME_CURRENT_SCAN);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;
                    ret = WaitForSarScanComplete();
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;

                    ret = ReadWord(0x12, ref wdata);
                    if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        return ret;

                    parent.m_OpRegImg[0x12].err = ret;
                    parent.m_OpRegImg[0x12].val = wdata;

                    ret = ReadCADC(ElementDefine.CADC_MODE.MOVING);
                    break;
                #endregion
            }

            return ret;
        }

        public UInt32 EpBlockRead()
        {
            ushort wdata = 0;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            ret = ReadWord(0x58, ref wdata);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;
            ret = WriteWord(0x58, (ushort)(wdata | 0x0100));
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;
            return ret;
        }
        #endregion

        #region 特殊服务功能设计
        public UInt32 GetDeviceInfor(ref DeviceInfor deviceinfor)
        {
#if debug
            return LibErrorCode.IDS_ERR_SUCCESSFUL;
#else
            string shwversion = String.Empty;
            UInt16 wval = 0;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            ret = ReadWord(0x00, ref wval);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL) return ret;

            deviceinfor.status = 0;
            deviceinfor.type = wval;

            foreach (UInt16 type in deviceinfor.pretype)
            {
                ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
                if (type != deviceinfor.type)
                    ret = LibErrorCode.IDS_ERR_DEM_BETWEEN_SELECT_BOARD;

                if (ret == LibErrorCode.IDS_ERR_SUCCESSFUL) break;
            }

            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                LibErrorCode.UpdateDynamicalErrorDescription(ret, new string[] { deviceinfor.shwversion });

            return ret;
#endif
        }

        public UInt32 GetSystemInfor(ref TASKMessage msg)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            msg.sm.dic.Clear();
            UInt32 cellnum = (UInt32)parent.CellNum.phydata + 10;    //0~7 means 7~14
            if (cellnum == 17)
            {
                for (byte i = 0; i < 17; i++)
                    msg.sm.dic.Add((uint)(i), true);
            }
            else
            {
                for (byte i = 0; i < 17; i++)
                {
                    if (i < cellnum - 1)
                        msg.sm.dic.Add((uint)i, true);
                    else if (i == cellnum - 1)
                        msg.sm.dic.Add(16, false);
                    else if (i < 16)
                        msg.sm.dic.Add((uint)i, false);
                    else if (i == 16)
                        msg.sm.dic.Add(cellnum - 1, true);
                }
            }

            return ret;
        }

        public UInt32 GetRegisteInfor(ref TASKMessage msg)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            return ret;
        }
        #endregion
    }
}