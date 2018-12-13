using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using O2Micro.Cobra.Common;

namespace O2Micro.Cobra.KALL17
{
    internal class DEMDataManage
    {
        //父对象保存
        private DEMDeviceManage m_parent;
        public DEMDeviceManage parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }

        bool FromHexToPhy = false;
        /// <summary>
        /// 硬件模式下相关参数数据初始化
        /// </summary>
        public void Init(object pParent)
        {
                        
            parent = (DEMDeviceManage)pParent;
            if (parent.EFParamlist == null) return;
        }

        private void UpdateThType(ref Parameter pTH)
        {
            Parameter pEnable = new Parameter();
            double tmp = 0;
            ushort wdata = 0;
            switch (pTH.guid)
            {
                case ElementDefine.OVP_H:
                    pEnable = parent.OVP_E;
                    break;
                case ElementDefine.DOC1P:
                    pEnable = parent.DOC1P_E;
                    break;
                case ElementDefine.COCP:
                    pEnable = parent.COCP_E;
                    break;
            }
            if (pEnable.phydata == 0)
            {
                wdata = 0;
                tmp = Hex2Volt(wdata, pTH.offset, pTH.regref, pTH.phyref);
                pTH.phydata = tmp;
            }
            else if (pEnable.phydata == 1)
            {
                wdata = 0;
                tmp = Hex2Volt(wdata, pTH.offset, pTH.regref, pTH.phyref);
                if (pTH.phydata == tmp)
                {
                    wdata = 1;
                    tmp = Hex2Volt(wdata, pTH.offset, pTH.regref, pTH.phyref);
                    pTH.phydata = tmp;
                }
            }
        }

        private void UpdateEnableType(ref Parameter pEnable)
        {
            Parameter source = new Parameter();
            ushort wdata = 0;
            double tmp = 0;
            switch (pEnable.guid)
            {
                case ElementDefine.OVP_E:
                    source = parent.OVP_H;
                    break;
                case ElementDefine.DOC1P_E:
                    source = parent.DOC1P;
                    break;
                case ElementDefine.COCP_E:
                    source = parent.COCP;
                    break;
            }

            wdata = 0;
            tmp = Hex2Volt(wdata, source.offset, source.regref, source.phyref);
            if (source.phydata == tmp)
            {
                pEnable.phydata = 0;
            }
            else
                pEnable.phydata = 1;
        }

        /// <summary>
        /// 更新参数ItemList
        /// </summary>
        /// <param name="p"></param>
        /// <param name="relatedparameters"></param>
        /// <returns></returns>
        public void UpdateEpParamItemList(Parameter pTarget)
        {
            if (pTarget.errorcode != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return;
            Parameter source = new Parameter();
            switch (pTarget.guid)
            {
                case ElementDefine.OVP_E:
                case ElementDefine.DOC1P_E:
                case ElementDefine.COCP_E:
                    UpdateEnableType(ref pTarget);
                    break;
                case ElementDefine.OVP_H:
                case ElementDefine.DOC1P:
                case ElementDefine.COCP:
                    UpdateThType(ref pTarget);
                    break;
            }
            FromHexToPhy = false;
            return;
        }

        private double Volt2Temp(double volt)
        {
            double Thm_PullupRes = 100, temp;
            volt = (double)((volt * (Thm_PullupRes) * 1000) / (2000 - volt));
            temp = ResistToTemp(volt);
            return temp;
        }
        private double Temp2Volt(double temp)
        {

            double Thm_PullupRes = 100, volt;

            volt = TempToResist(temp);
            volt = volt * Thm_PullupRes * 20 / (volt + Thm_PullupRes * 1000);   //20是电流
            return volt;
        }

        private ushort Volt2Hex(double volt, double offset, double regref, double phyref)
        {
            ushort hex;
            volt -= offset;
            volt = volt * regref / phyref; 
            hex = (UInt16)Math.Round(volt);
            return hex;
        }
        private double Hex2Volt(ushort hex, double offset, double regref, double phyref)
        {
            double volt;
            volt = (double)((double)hex * phyref / regref);
            volt += offset;//voltage
            return volt;
        }

        /// <summary>
        /// 转换参数值类型从物理值到16进制值
        /// </summary>
        /// <param name="p"></param>
        /// <param name="relatedparameters"></param>
        public void Physical2Hex(ref Parameter p)
        {
            UInt16 wdata = 0;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            if (p == null) return;
            switch ((ElementDefine.SUBTYPE)p.subtype)
            {
                case ElementDefine.SUBTYPE.VOLTAGE:
                    {
                        wdata = Physical2Regular((float)p.phydata, p.regref, p.phyref);
                        ret = WriteToRegImg(p, wdata);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            WriteToRegImgError(p, ret);
                        break;
                    }
                case ElementDefine.SUBTYPE.EXT_TEMP://温度是只读参数，这里用不到
                    {

                        break;
                    }
                case ElementDefine.SUBTYPE.INT_TEMP:
                    {
                        break;
                    }
                case ElementDefine.SUBTYPE.EXT_TEMP_TABLE:
                case ElementDefine.SUBTYPE.INT_TEMP_REFER:
                    {
                        m_parent.ModifyTemperatureConfig(p, true);
                        break;
                    }
                case ElementDefine.SUBTYPE.CURRENT:
                    {
                        break;
                    }
                case ElementDefine.SUBTYPE.PRE_SET:
                    {
                        p.phydata = p.phydata * parent.etrx;
                        p.phydata -= 100;

                        //wdata = Physical2Regular((float)p.phydata, p.regref, p.phyref);
                        wdata = (ushort)(p.phydata * p.regref / p.phyref);
                        ret = WriteToRegImg(p, wdata);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            WriteToRegImgError(p, ret);
                        break;
                    }
                default:
                    {
                        double tmp = p.phydata - p.offset;
                        tmp = tmp * p.regref;
                        tmp = tmp / p.phyref;
                        double res = tmp % 1;
                        if (res < 0.99)
                            wdata = (UInt16)(tmp);
                        else
                        {
                            wdata = (UInt16)(tmp);
                            wdata += 1;
                        }
                        ret = WriteToRegImg(p, wdata);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                            WriteToRegImgError(p, ret);
                        break;
                    }
            }
        }

        /// <summary>
        /// 转换参数值类型从物理值到16进制值
        /// </summary>
        /// <param name="p"></param>
        /// <param name="relatedparameters"></param>
        public void Hex2Physical(ref Parameter p)
        {
            UInt16 wdata = 0;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            double dtmp = 0.0;

            if (p == null) return;
            switch ((ElementDefine.SUBTYPE)p.subtype)
            {
                case ElementDefine.SUBTYPE.VOLTAGE:
                    {
                        ret = ReadFromRegImg(p, ref wdata);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        {
                            p.phydata = ElementDefine.PARAM_PHYSICAL_ERROR;
                            break;
                        }
                        if (wdata == ElementDefine.PARAM_HEX_ERROR)
                            p.phydata = ElementDefine.PARAM_PHYSICAL_ERROR;
                        else
                        {
                            short s = (short)wdata;
                            p.phydata = s * p.phyref / p.regref;
                        }

                        break;
                    }
                case ElementDefine.SUBTYPE.WKUP:
                    {
                        if (parent.m_Vwkup.val == ElementDefine.PARAM_HEX_ERROR)
                        {
                            p.hexdata = ElementDefine.PARAM_HEX_ERROR;
                            p.phydata = ElementDefine.PARAM_PHYSICAL_ERROR;
                        }
                        else
                        {
                            wdata = parent.m_Vwkup.val;
                            p.hexdata = wdata;
                            short s = (short)wdata;
                            p.phydata = s * p.phyref / p.regref;
                        }

                        break;
                    }
                case ElementDefine.SUBTYPE.CURRENT:
                    {
                        ret = ReadFromRegImg(p, ref wdata);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        {
                            p.phydata = ElementDefine.PARAM_PHYSICAL_ERROR;
                            break;
                        }
                        short s = (short)wdata;
                        //s = (short)(-s);      //MP version don't need to change the sign
                        p.phydata = s * p.phyref * 1000 / parent.etrx; //需要带符号
                        break;
                    }
                case ElementDefine.SUBTYPE.CADC:
                    {
                        if (parent.cadc_mode == ElementDefine.CADC_MODE.DISABLE)
                            wdata = 0;
                        else if (parent.cadc_mode == ElementDefine.CADC_MODE.TRIGGER)
                        {
                            wdata = parent.m_OpRegImg[0x38].val;
                            ret = parent.m_OpRegImg[0x38].err;
                        }
                        else if (parent.cadc_mode == ElementDefine.CADC_MODE.MOVING)
                        {
                            wdata = parent.m_OpRegImg[0x39].val;
                            ret = parent.m_OpRegImg[0x39].err;
                        }
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        {
                            p.phydata = ElementDefine.PARAM_PHYSICAL_ERROR;
                            break;
                        }
                        short s = (short)wdata;
                        p.phydata = s * p.phyref * 1000 / parent.etrx; //需要带符号
                        break;
                    }
                case ElementDefine.SUBTYPE.COULOMB_COUNTER:
                    {
                        Int32 ddata = 0;
                        ret = ReadFromRegImg(p, ref ddata);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        {
                            p.phydata = ElementDefine.PARAM_PHYSICAL_ERROR;
                            break;
                        }
                        dtmp = (double)((double)ddata * p.phyref / p.regref);   //uVs
                        dtmp = dtmp / parent.etrx;  //mAs
                        dtmp /= 3600;               //mAH
                        p.phydata = dtmp;
                        break;
                    }
                case ElementDefine.SUBTYPE.EXT_TEMP:
                    {
                        int index = 0;
                        switch (p.guid)
                        {
                            case 0x00031300: index = 0; break;
                            case 0x00031400: index = 1; break;
                            case 0x00031500: index = 2; break;
                            case 0x00031600: index = 3; break;
                            case 0x00031700: index = 4; break;
                        }

                        ret = ReadFromRegImg(p, ref wdata);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        {
                            p.phydata = ElementDefine.PARAM_PHYSICAL_ERROR;
                            break;
                        }
                        ushort Cref = 0;
                        Cref = parent.thms[index].thm_crrt;
                        dtmp = Regular2Physical(wdata, p.regref, p.phyref);
                        dtmp = dtmp * 1000 / Cref;
                        p.phydata = ResistToTemp(dtmp);
                        break;
                    }
                case ElementDefine.SUBTYPE.INT_TEMP:
                    {
                        ret = ReadFromRegImg(p, ref wdata);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        {
                            p.phydata = ElementDefine.PARAM_PHYSICAL_ERROR;
                            break;
                        }
                        dtmp = Regular2Physical(wdata, p.regref, p.phyref); //Vt
                        p.phydata = (dtmp - 1252.5) / 4.345 + 23.0;

                        break;
                    }
                case ElementDefine.SUBTYPE.PRE_SET:
                    {
                        ret = ReadFromRegImg(p, ref wdata);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        {
                            p.phydata = ElementDefine.PARAM_PHYSICAL_ERROR;
                            break;
                        }
                        dtmp = Regular2Physical(wdata, p.regref, p.phyref);
                        dtmp += 100;
                        p.phydata = dtmp / parent.etrx;
                        break;
                    }
                case ElementDefine.SUBTYPE.INT_TEMP_REFER:
                case ElementDefine.SUBTYPE.EXT_TEMP_TABLE:
                    {
                        m_parent.ModifyTemperatureConfig(p, false);
                        break;
                    }
                default:
                    {
                        ret = ReadFromRegImg(p, ref wdata);
                        if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                        {
                            p.phydata = ElementDefine.PARAM_PHYSICAL_ERROR;
                            break;
                        }
                        dtmp = (double)((double)wdata * p.phyref / p.regref);
                        p.phydata = dtmp + p.offset;
                        break;
                    }
            }
            FromHexToPhy = true;
        }

        /// <summary>
        /// 转换Hex -> Physical
        /// </summary>
        /// <param name="sVal"></param>
        /// <param name="RegularRef"></param>
        /// <param name="PhysicalRef"></param>
        /// <returns></returns>
        private double Regular2Physical(UInt16 wVal, double RegularRef, double PhysicalRef)
        {
            double dval;

            dval = (double)((double)(wVal * PhysicalRef) / (double)RegularRef);

            return dval;
        }

        /// <summary>
        /// 转换Physical -> Hex
        /// </summary>
        /// <param name="fVal"></param>
        /// <param name="RegularRef"></param>
        /// <param name="PhysicalRef"></param>
        /// <returns></returns>
        private UInt16 Physical2Regular(float fVal, double RegularRef, double PhysicalRef)
        {
            UInt16 wval;
            double dval, integer, fraction;

            dval = (double)((double)(fVal * RegularRef) / (double)PhysicalRef);
            integer = Math.Truncate(dval);
            fraction = (double)(dval - integer);
            if (fraction >= 0.5)
                integer += 1;
            if (fraction <= -0.5)
                integer -= 1;
            wval = (UInt16)integer;

            return wval;
        }

        /// <summary>
        /// 从数据buffer中读数据
        /// </summary>
        /// <param name="pval"></param>
        /// <returns></returns>
        public UInt32 ReadFromRegImg(Parameter p, ref UInt16 pval)
        {
            UInt32 data;
            UInt16 hi = 0, lo = 0;
            Reg regLow = null, regHi = null;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            foreach (KeyValuePair<string, Reg> dic in p.reglist)
            {
                if (dic.Key.Equals("Low"))
                {
                    regLow = dic.Value;
                    ret = ReadRegFromImg(regLow.address, p.guid, ref lo);
                    lo <<= (16 - regLow.bitsnumber - regLow.startbit); //align with left
                }
                else if (dic.Key.Equals("High"))
                {
                    regHi = dic.Value;
                    ret = ReadRegFromImg(regHi.address, p.guid, ref hi);
                    hi <<= (16 - regHi.bitsnumber - regHi.startbit); //align with left
                    hi >>= (16 - regHi.bitsnumber); //align with right
                }
            }

            data = ((UInt32)(((UInt16)(lo)) | ((UInt32)((UInt16)(hi))) << 16));
            data >>= (16 - regLow.bitsnumber); //align with right

            pval = (UInt16)data;
            p.hexdata = pval;
            return ret;
        }

        public UInt32 ReadFromRegImg(Parameter p, ref Int32 pval)
        {
            UInt32 data;
            UInt16 hi = 0, lo = 0;
            Reg regLow = null, regHi = null;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            foreach (KeyValuePair<string, Reg> dic in p.reglist)
            {
                if (dic.Key.Equals("Low"))
                {
                    regLow = dic.Value;
                    ret = ReadRegFromImg(regLow.address, p.guid, ref lo);
                    lo <<= (16 - regLow.bitsnumber - regLow.startbit); //align with left
                }
                else if (dic.Key.Equals("High"))
                {
                    regHi = dic.Value;
                    ret = ReadRegFromImg(regHi.address, p.guid, ref hi);
                    hi <<= (16 - regHi.bitsnumber - regHi.startbit); //align with left
                    hi >>= (16 - regHi.bitsnumber); //align with right
                }
            }

            data = ((UInt32)(((UInt16)(lo)) | ((UInt32)((UInt16)(hi))) << 16));
            data >>= (16 - regLow.bitsnumber); //align with right

            pval = (Int32)data;
            p.hexdata = (UInt16)pval;
            return ret;
        }

        /// <summary>
        /// 从数据buffer中读有符号数
        /// </summary>
        /// <param name="pval"></param>
        /// <returns></returns>
        private UInt32 ReadSignedFromRegImg(Parameter p, ref short pval)
        {
            UInt16 wdata = 0, tr = 0;
            Int16 sdata;
            Reg regLow = null, regHi = null;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            ret = ReadFromRegImg(p, ref wdata);
            if (ret != LibErrorCode.IDS_ERR_SUCCESSFUL)
                return ret;

            foreach (KeyValuePair<string, Reg> dic in p.reglist)
            {
                if (dic.Key.Equals("Low"))
                    regLow = dic.Value;

                if (dic.Key.Equals("High"))
                    regHi = dic.Value;
            }

            if (regHi != null)
                tr = (UInt16)(16 - regHi.bitsnumber - regLow.bitsnumber);
            else
                tr = (UInt16)(16 - regLow.bitsnumber);

            wdata <<= tr;
            sdata = (Int16)wdata;
            sdata = (Int16)(sdata / (1 << tr));

            pval = sdata;
            return ret;
        }


        /// <summary>
        /// 写数据到buffer中
        /// </summary>
        /// <param name="wVal"></param>
        /// <returns></returns>
        public UInt32 WriteToRegImg(Parameter p, UInt16 wVal)
        {
            UInt16 data = 0, lomask = 0, himask = 0;
            UInt16 plo, phi, ptmp;
            //byte hi = 0, lo = 0, tmp = 0;
            Reg regLow = null, regHi = null;
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;

            p.hexdata = wVal;
            foreach (KeyValuePair<string, Reg> dic in p.reglist)
            {
                if (dic.Key.Equals("Low"))
                    regLow = dic.Value;

                if (dic.Key.Equals("High"))
                    regHi = dic.Value;
            }

            ret = ReadRegFromImg(regLow.address, p.guid, ref data);
            if (regHi == null)
            {
                lomask = (UInt16)((1 << regLow.bitsnumber) - 1);
                lomask <<= regLow.startbit;
                data &= (UInt16)(~lomask);
                data |= (UInt16)(wVal << regLow.startbit);
                WriteRegToImg(regLow.address, p.guid, data);
            }
            else
            {

                lomask = (UInt16)((1 << regLow.bitsnumber) - 1);
                plo = (UInt16)(wVal & lomask);
                himask = (UInt16)((1 << regHi.bitsnumber) - 1);
                himask <<= regLow.bitsnumber;
                phi = (UInt16)((wVal & himask) >> regLow.bitsnumber);

                //mask = (UInt16)((1 << regLow.bitsnumber) - 1);
                lomask <<= regLow.startbit;
                ptmp = (UInt16)(data & ~lomask);
                ptmp |= (UInt16)(plo << regLow.startbit);
                WriteRegToImg(regLow.address, p.guid, ptmp);

                ret |= ReadRegFromImg(regHi.address, p.guid, ref data);
                himask = (UInt16)((1 << regHi.bitsnumber) - 1);
                himask <<= regHi.startbit;
                ptmp = (UInt16)(data & ~himask);
                ptmp |= (UInt16)(phi << regHi.startbit);
                WriteRegToImg(regHi.address, p.guid, ptmp);

            }

            return ret;
        }


        /// <summary>
        /// 写有符号数据到buffer中
        /// </summary>
        /// <param name="wVal"></param>
        /// <param name="pChip"></param>
        /// <returns></returns>
        private UInt32 WriteSignedToRegImg(Parameter p, Int16 sVal)
        {
            UInt16 wdata, tr = 0;
            Int16 sdata;
            Reg regLow = null, regHi = null;

            sdata = sVal;
            foreach (KeyValuePair<string, Reg> dic in p.reglist)
            {
                if (dic.Key.Equals("Low"))
                    regLow = dic.Value;

                if (dic.Key.Equals("High"))
                    regHi = dic.Value;
            }
            if (regHi != null)
                tr = (UInt16)(16 - regHi.bitsnumber - regLow.bitsnumber);
            else
                tr = (UInt16)(16 - regLow.bitsnumber);

            sdata *= (Int16)(1 << tr);
            wdata = (UInt16)sdata;
            wdata >>= tr;

            return WriteToRegImg(p, wdata);
        }

        private void WriteToRegImgError(Parameter p, UInt32 err)
        {
        }

        #region EFuse数据缓存操作
        private UInt32 ReadRegFromImg(UInt16 reg, UInt32 guid, ref UInt16 pval)
        {
            UInt32 ret = LibErrorCode.IDS_ERR_SUCCESSFUL;
            switch (guid & ElementDefine.ElementMask)
            {
                case ElementDefine.EFUSEElement:
                    {
                        pval = parent.m_EFRegImg[reg].val;
                        ret = parent.m_EFRegImg[reg].err;
                        break;
                    }
                case ElementDefine.OperationElement:
                    {
                        pval = parent.m_OpRegImg[reg].val;
                        ret = parent.m_OpRegImg[reg].err;
                        break;
                    }
                default:
                    break;
            }
            return ret;
        }

        private void WriteRegToImg(UInt16 reg, UInt32 guid, UInt16 value)
        {
            switch (guid & ElementDefine.ElementMask)
            {
                case ElementDefine.EFUSEElement:
                    {
                        parent.m_EFRegImg[reg].val = value;
                        parent.m_EFRegImg[reg].err = LibErrorCode.IDS_ERR_SUCCESSFUL;
                        break;
                    }
                case ElementDefine.OperationElement:
                    {
                        parent.m_OpRegImg[reg].val = value;
                        parent.m_OpRegImg[reg].err = LibErrorCode.IDS_ERR_SUCCESSFUL;
                        break;
                    }
                default:
                    break;
            }
        }
        #endregion

        #region 外部温度转换
        public double ResistToTemp(double resist)
        {
            int index = 0;
            Dictionary<Int32, double> m_TempVals = new Dictionary<int, double>();
            Dictionary<Int32, double> m_ResistVals = new Dictionary<int, double>();
            if (parent.tempParamlist == null) return 0;

            foreach (Parameter p in parent.tempParamlist.parameterlist)
            {
                //利用温度参数属性下subtype区分内部/外部温度
                //0:内部温度参数 1： 外部温度参数
                if ((ElementDefine.SUBTYPE)p.subtype == ElementDefine.SUBTYPE.EXT_TEMP_TABLE)
                {
                    m_TempVals.Add(index, p.key);
                    m_ResistVals.Add(index, p.phydata);
                    index++;
                }
            }
            return SharedFormula.ResistToTemp(resist, m_TempVals, m_ResistVals);
        }

        public double TempToResist(double temp)
        {
            int index = 0;
            Dictionary<Int32, double> m_TempVals = new Dictionary<int, double>();
            Dictionary<Int32, double> m_ResistVals = new Dictionary<int, double>();
            if (parent.tempParamlist == null) return 0;

            foreach (Parameter p in parent.tempParamlist.parameterlist)
            {
                //利用温度参数属性下subtype区分内部/外部温度
                //0:内部温度参数 1： 外部温度参数
                if ((ElementDefine.SUBTYPE)p.subtype == ElementDefine.SUBTYPE.EXT_TEMP_TABLE)
                {
                    m_TempVals.Add(index, p.key);
                    m_ResistVals.Add(index, p.phydata);
                    index++;
                }
            }

            return SharedFormula.TempToResist(temp, m_TempVals, m_ResistVals);
        }
        #endregion
    }
}
