using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Reflection;
using O2Micro.Cobra.Common;
using O2Micro.Cobra.AutoMationTest;

namespace O2Micro.Cobra.KALL17
{
    public class DEMDeviceManage : IDEMLib
    {
        #region Properties

        public bool isAMTEnabled
        {
            get { return (m_busoption.GetATMElementbyGuid(AutomationElement.GUIDATMTestStart).dbValue > 0); }
        }

        private double m_EtRx;
        internal double etrx
        {
            get
            {
                Parameter param = tempParamlist.GetParameterByGuid(ElementDefine.TpETRx);
                if (param == null) return 0.0;
                else return param.phydata;
                //return m_PullupR; 
            }
            //set { m_PullupR = value; }
        }

        internal ParamContainer EFParamlist = null;
        internal ParamContainer OPParamlist = null;
        internal ParamContainer tempParamlist = null;

        internal BusOptions m_busoption = null;
        internal DeviceInfor m_deviceinfor = null;
        internal ParamListContainer m_Section_ParamlistContainer = null;
        internal ParamListContainer m_SFLs_ParamlistContainer = null;

        internal COBRA_HWMode_Reg[] m_EFRegImg = new COBRA_HWMode_Reg[ElementDefine.EF_MEMORY_SIZE + ElementDefine.EF_MEMORY_OFFSET];
        internal COBRA_HWMode_Reg[] m_EFRegImgEX = new COBRA_HWMode_Reg[ElementDefine.EF_MEMORY_SIZE];
        internal COBRA_HWMode_Reg[] m_OpRegImg = new COBRA_HWMode_Reg[ElementDefine.OP_MEMORY_SIZE];
        internal COBRA_HWMode_Reg m_Vwkup = new COBRA_HWMode_Reg();
        private Dictionary<UInt32, COBRA_HWMode_Reg[]> m_HwMode_RegList = new Dictionary<UInt32, COBRA_HWMode_Reg[]>();

        private DEMBehaviorManage m_dem_bm = new DEMBehaviorManage();
        private DEMDataManage m_dem_dm = new DEMDataManage();



        public Parameter CellNum = new Parameter();
        public Parameter[] Cell = new Parameter[17];

        public Parameter OVP_H = new Parameter();
        public Parameter DOC1P = new Parameter();
        public Parameter COCP = new Parameter();
        public Parameter OVP_E = new Parameter();
        public Parameter DOC1P_E = new Parameter();
        public Parameter COCP_E = new Parameter();

        public bool isOVPEnabled = true;
        public bool isDOC1PEnabled = true;
        public bool isCOCEnabled = true;

        public ElementDefine.CADC_MODE cadc_mode = ElementDefine.CADC_MODE.DISABLE;

        public struct THM
        {
            public ushort ADC1;
            public ushort ADC2;
            public ushort max;
            public ushort min;
            public ushort thm_crrt;
        }

        public THM[] thms = new THM[5];

        #region Dynamic ErrorCode
        public Dictionary<UInt32, string> m_dynamicErrorLib_dic = new Dictionary<uint, string>()
        {
            {ElementDefine.IDS_ERR_DEM_READCADC_TIMEOUT,"Read CADC timeout!"},
            {ElementDefine.IDS_ERR_DEM_WAIT_TRIGGER_FLAG_TIMEOUT,"Wait trigger scan flag timeout!"},
            {ElementDefine.IDS_ERR_DEM_ACTIVE_MODE_ERROR,"Not in Active mode, please check."},
            {ElementDefine.IDS_ERR_DEM_CFET_ON_FAILED,"Cannot turn on CFET. Please check 1. if there is any OV or COC event 2. EFETC mode setting 3. EFETC pin status."},
            {ElementDefine.IDS_ERR_DEM_CFET_OFF_FAILED,"Cannot turn off CFET. Please check if it is in discharging state."},
            {ElementDefine.IDS_ERR_DEM_DFET_ON_FAILED,"Cannot turn on DFET. Please check 1. if there is any DOC or SC event 2. EFETC mode setting 3. EFETC pin status."},
            {ElementDefine.IDS_ERR_DEM_DFET_OFF_FAILED,"Cannot turn off DFET. Please check if it is in charging state."},
        };
        #endregion
        #endregion


        private void InitParameters()
        {
            ParamContainer pc = m_Section_ParamlistContainer.GetParameterListByGuid(ElementDefine.OperationElement);
            OVP_H = pc.GetParameterByGuid(ElementDefine.OVP_H);
            DOC1P = pc.GetParameterByGuid(ElementDefine.DOC1P);
            COCP = pc.GetParameterByGuid(ElementDefine.COCP);
            pc = m_Section_ParamlistContainer.GetParameterListByGuid(ElementDefine.VirtualElement);
            OVP_E = pc.GetParameterByGuid(ElementDefine.OVP_E);
            DOC1P_E = pc.GetParameterByGuid(ElementDefine.DOC1P_E);
            COCP_E = pc.GetParameterByGuid(ElementDefine.COCP_E);

            CellNum = m_Section_ParamlistContainer.GetParameterListByGuid(ElementDefine.OperationElement).GetParameterByGuid(ElementDefine.CellNum);
            for (int i = 0; i < 17; i++)
            {
                Cell[i] = m_Section_ParamlistContainer.GetParameterListByGuid(ElementDefine.OperationElement).GetParameterByGuid(ElementDefine.CellBase + (UInt32)(i * 0x100));
            }

        }

        public void Physical2Hex(ref Parameter param)
        {
            m_dem_dm.Physical2Hex(ref param);
        }

        public void Hex2Physical(ref Parameter param)
        {
            m_dem_dm.Hex2Physical(ref param);
        }

        private void SectionParameterListInit(ref ParamListContainer devicedescriptionlist)
        {
            tempParamlist = devicedescriptionlist.GetParameterListByGuid(ElementDefine.TemperatureElement);
            if (tempParamlist == null) return;

            EFParamlist = devicedescriptionlist.GetParameterListByGuid(ElementDefine.EFUSEElement);
            if (EFParamlist == null) return;

            OPParamlist = devicedescriptionlist.GetParameterListByGuid(ElementDefine.OperationElement);
            if (EFParamlist == null) return;

            //pullupR = tempParamlist.GetParameterByGuid(ElementDefine.TpETPullupR).phydata;
            //itv0 = tempParamlist.GetParameterByGuid(ElementDefine.TpITSlope).phydata;
        }

        public void ModifyTemperatureConfig(Parameter p, bool bConvert)
        {
            //bConvert为真 physical ->hex;假 hex->physical;
            Parameter tmp = tempParamlist.GetParameterByGuid(p.guid);
            if (tmp == null) return;
            if (bConvert)
                tmp.phydata = p.phydata;
            else
                p.phydata = tmp.phydata;
        }

        private void InitialImgReg()
        {
            for (byte i = 0; i < ElementDefine.EF_MEMORY_SIZE; i++)
            {
                m_EFRegImgEX[i] = new COBRA_HWMode_Reg();
                m_EFRegImgEX[i].val = ElementDefine.PARAM_HEX_ERROR;
                m_EFRegImgEX[i].err = LibErrorCode.IDS_ERR_BUS_DATA_PEC_ERROR;

                m_EFRegImg[i + ElementDefine.EF_MEMORY_OFFSET] = m_EFRegImgEX[i];
            }

            for (byte i = 0; i < ElementDefine.OP_MEMORY_SIZE; i++)
            {
                m_OpRegImg[i] = new COBRA_HWMode_Reg();
                m_OpRegImg[i].val = ElementDefine.PARAM_HEX_ERROR;
                m_OpRegImg[i].err = LibErrorCode.IDS_ERR_BUS_DATA_PEC_ERROR;
            }
            m_Vwkup.val = ElementDefine.PARAM_HEX_ERROR;
            m_Vwkup.err = LibErrorCode.IDS_ERR_BUS_DATA_PEC_ERROR;
        }

        public UInt32 ReadFromRegImg(Parameter p, ref UInt16 wVal)
        {
            return m_dem_dm.ReadFromRegImg(p, ref wVal);
        }

        public UInt32 WriteToRegImg(Parameter p, UInt16 wVal)
        {
            return m_dem_dm.WriteToRegImg(p, wVal);
        }

		#region 接口实现
        public void Init(ref BusOptions busoptions, ref ParamListContainer deviceParamlistContainer, ref ParamListContainer sflParamlistContainer)
        {
            m_busoption = busoptions;
            m_Section_ParamlistContainer = deviceParamlistContainer;
            m_SFLs_ParamlistContainer = sflParamlistContainer;
            SectionParameterListInit(ref deviceParamlistContainer);

            m_HwMode_RegList.Add(ElementDefine.EFUSEElement, m_EFRegImg);
            m_HwMode_RegList.Add(ElementDefine.OperationElement, m_OpRegImg);
            AutoMationTest.AutoMationTest.init(m_HwMode_RegList);

            SharedAPI.ReBuildBusOptions(ref busoptions, ref deviceParamlistContainer);

            InitialImgReg();
            InitParameters();

            m_dem_bm.Init(this);
            m_dem_dm.Init(this);
            LibInfor.AssemblyRegister(Assembly.GetExecutingAssembly(), ASSEMBLY_TYPE.DEM);
            LibErrorCode.UpdateDynamicalLibError(ref m_dynamicErrorLib_dic);
        }

        public bool EnumerateInterface()
        {
            return m_dem_bm.EnumerateInterface();
        }

        public bool CreateInterface()
        {
            return m_dem_bm.CreateInterface();
        }

        public bool DestroyInterface()
        {
            return m_dem_bm.DestroyInterface();
        }

        public void UpdataDEMParameterList(Parameter p)
        {
            m_dem_dm.UpdateEpParamItemList(p);
        }

        public UInt32 GetDeviceInfor(ref DeviceInfor deviceinfor)
        {
            return m_dem_bm.GetDeviceInfor(ref deviceinfor);
        }

        public UInt32 Erase(ref TASKMessage bgworker)
        {
            return m_dem_bm.EraseEEPROM(ref bgworker);
        }

        public UInt32 BlockMap(ref TASKMessage bgworker)
        {
            return m_dem_bm.EpBlockRead();
        }

        public UInt32 Command(ref TASKMessage bgworker)
        {
            return m_dem_bm.Command(ref bgworker);
        }

        public UInt32 Read(ref TASKMessage bgworker)
        {
            return m_dem_bm.Read(ref bgworker);
        }

        public UInt32 Write(ref TASKMessage bgworker)
        {
            return m_dem_bm.Write(ref bgworker);
        }

        public UInt32 BitOperation(ref TASKMessage bgworker)
        {
            return m_dem_bm.BitOperation(ref bgworker);
        }

        public UInt32 ConvertHexToPhysical(ref TASKMessage bgworker)
        {
            return m_dem_bm.ConvertHexToPhysical(ref bgworker);
        }

        public UInt32 ConvertPhysicalToHex(ref TASKMessage bgworker)
        {
            return m_dem_bm.ConvertPhysicalToHex(ref bgworker);
        }

        public UInt32 GetSystemInfor(ref TASKMessage bgworker)
        {
            return m_dem_bm.GetSystemInfor(ref bgworker);
        }

        public UInt32 GetRegisteInfor(ref TASKMessage bgworker)
        {
            return m_dem_bm.GetRegisteInfor(ref bgworker);
        }
        #endregion
    }
}

