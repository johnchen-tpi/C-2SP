using System;
using MySql.Data.MySqlClient;
using System.Configuration;

namespace Normalization_MS
{
    class main
    {
        public static int intPageDataCount = Convert.ToInt16(ConfigurationManager.AppSettings["pageDataCount"]);    //由 Temp DB 分批取得的筆數

        static void Main(string[] args)
        {
            //////// 轉置開始 //////
            Console.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + " 開始執行資料轉置...");

            #region -進行測試的code-
            //ComFacBizMatch.getDepartmentMapping();
            //TFadenBook.convertFunc("TFadenBook", intPageDataCount);
            //TCommonDangerItemTXG.convertFunc("TCommonDangerItemTXG");
            //TCommonDangerItemTPI.convertFunc("TCommonDangerItemTPI");
            //TCommonDangerItemTYC.convertFunc("TCommonDangerItemTYC");
            //TCommonDangerItemNTPC.convertFunc("TCommonDangerItemNTPC");

            //string AdminTemp = ComFacBizMatch.MainFunc("-", "", "-", "", "-", "-", "TFoodSafetyInquiry", "0");
            #endregion

            ///根據Parser的轉置紀錄，檢查今天需轉置的資料集共那些，將這些資料集的hasUnTransData設置成1
            if (DateTime.Now > Convert.ToDateTime(DateTime.Now.ToString("yyyy/MM/dd") + " 22:05:00") && DateTime.Now < Convert.ToDateTime(DateTime.Now.ToString("yyyy/MM/dd") + " 23:57:59"))
            {
                funcUtil.CheckParserDataSet();
            }

            /*if (DateTime.Now > Convert.ToDateTime(DateTime.Now.ToString("yyyy/MM/dd") + " 20:00:00") && DateTime.Now < Convert.ToDateTime(DateTime.Now.ToString("yyyy/MM/dd") + " 21:59:59"))
            {
                //(暫時)若現在時間超過當日晚上八點並小於當日晚上十點則不進行動作(八點Parser、九點ETL)。
                return;
            }*/

            //若同時執行的程式數超過二支，則不進行動作。
            if (funcUtil.synRunPrgmCount())
                return;

            //取得 DepartmentMapping 中，部會代碼(SDeptId)及 TempTableName 資料。
            ComFacBizMatch.getDepartmentMapping();

            string strMergeClass = "";  //本次執行欲轉置的 當前整併類別
            string strMergeStep = "";   //本次執行欲轉置的 當前整併階段
            funcUtil.getMergeClassAndStep(ref strMergeClass, ref strMergeStep); //取得當前整併類別及當前整併階段
            try
            {
                // 針對取出的 當前整併階段 進行各別的轉置，空白不轉置。
                switch (strMergeStep)
                {
                    case "XmlParser":
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);
                        break;

                    #region 第一次 Merge 環保署(EPA)
                    case "TMatLitterRept":
                        //執行轉置 TMatLitterRept (DataId：EPA000002)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TMatLitterRept.convertFunc(strMergeStep);  //轉置 事業原物料及廢棄物之使用與產出申報資料 資料集
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TSgwRemediationFee":
                        //執行轉置 TSgwRemediationFee (DataId：EPA000006)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TSgwRemediationFee.convertFunc(strMergeStep, intPageDataCount);  //轉置 土水整治費物質資料 資料集
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TWaterPollutionExam":
                        //執行轉置 TWaterPollutionExam (DataId：EPA000007)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TWaterPollutionExam.convertFunc(strMergeStep, intPageDataCount);    //轉置 水污定檢原物料使用量(化學雲資料) 資料集
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TAirPolSource":
                        //執行轉置 TAirPolSource (DataId：EPA000012)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TAirPolSource.convertFunc(strMergeStep);    //轉置 公私場所固定污染源指定物種之申報資料 資料集
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TEnvironmentMdc":
                        //執行轉置 TEnvironmentMdc (DataId：EPA000005)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TEnvironmentMdc.convertFunc(strMergeStep, intPageDataCount);    //轉置 環境用藥管理資料 資料集
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TToxChemiOperation":
                        //執行轉置 TToxChemiOperation (DataId：EPA000013)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TToxChemiOperation.convertFunc(strMergeStep, intPageDataCount);    //轉置 運作紀錄資料(毒性化學物質許可管理) 資料集              
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TToxChemiRestAmount":
                        //執行轉置 TToxChemiRestAmount (DataId：EPA000014)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TToxChemiRestAmount.convertFunc(strMergeStep, intPageDataCount);  //轉置 結餘量資料(毒性化學物質許可管理) 資料集
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TToxChemiCredential":
                        //執行轉置 TToxChemiCredential
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TToxChemiCredential.convertFunc(strMergeStep);  //轉置 證件資料(毒性化學物質許可管理) 資料集
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TExistingChemical":
                        //執行轉置 TExistingChemical (DataId：EPA000016)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TExistingChemical.convertFunc(strMergeStep, intPageDataCount);  //轉置 既有化學物質資料 資料集
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "CHEMIST_MAIN":
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TNewChemical.convertFunc(strMergeStep, intPageDataCount);  //轉置 新化學物質 資料集
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TRmsBasicInfo":
                        //執行轉置 TRmsBasicInfo (DataId：EPA000019)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TRmsBasicInfo.convertFunc(strMergeStep);  //轉置 資源再利用管理資料(產品基本資料) 資料集
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TRmsOperation":
                        //執行轉置 TRmsOperation (DataId：EPA000018)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TRmsOperation.convertFunc(strMergeStep);  //轉置 資源再利用管理資料(生產、銷售及庫存申報情形) 資料集
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TRmsFlowApply":
                        //執行轉置 TRmsFlowApply (DataId：EPA000020)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TRmsFlowApply.convertFunc(strMergeStep);  //轉置 資源再利用管理資料(資源化產品銷售流向申報) 資料集
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TClearanceSignify801":
                        //執行轉置 TRmsFlowApply (DataId：EPA000022)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TClearanceSignify801.convertFunc(strMergeStep);  //轉置 801通關簽審資料 資料集
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TMonitor":
                        //執行轉置 TRmsFlowApply (DataId：EPA000023~EPA000035)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TMonitor.MainFunc();  //轉置 監測 測站 資料集
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TFoodSafetyInquiry":
                        //執行轉置 TFoodSafetyInquiry (DataId：EPA000036)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TFoodSafetyInquiry.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TInspectionHighRisk107":
                        //執行轉置 TInspectionHighRisk107 (DataId：EPA000037)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TInspectionHighRisk107.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TFoodSafetyInquiry108":
                        //執行轉置 TFoodSafetyInquiry108 (DataId：EPA000041)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TFoodSafetyInquiry108.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TEnvironMdcFlow":
                        //執行轉置 TEnvironMdcFlow (DataId：EPA000047)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TEnvironMdcFlow.convertFunc(strMergeStep, intPageDataCount);    //轉置 環境用藥紀錄流向資訊 資料集
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TFoodSafetyInquiry109":
                        //執行轉置 TFoodSafetyInquiry108 (DataId：EPA000048)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TFoodSafetyInquiry109.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TConcernPermits":
                        //執行轉置 TConcernPermits (DataId：EPA000049)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TConcernPermits.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TConcernOperation":
                        //執行轉置 TConcernPermits (DataId：EPA000050)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TConcernOperation.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TConcernOperationR":
                        //執行轉置 TConcernPermits (DataId：EPA000051)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TConcernOperationR.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TPunishment":
                        //執行轉置 TConcernPermits (DataId：EPA000040)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TPunishment.convertFunc(strMergeStep);
                        TPunishment.setAdmin("ExplosiveMergeData");
                        TPunishment.setAdmin("HazardousMergeData");
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TDrinkingWater":
                        //執行轉置 TDrinkingWater
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TDrinkingWater.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);
                        break;
                    case "TEmsBasicInfo":
                        //執行轉置 TEmsBasicInfo
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TEmsBasicInfo.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TSgwBasicInfo":
                        //執行轉置 TEmsBasicInfo
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TSgwBasicInfo.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TSgwProduct":
                        //執行轉置 TEmsBasicInfo
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TSgwProduct.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TSgwMaterial":
                        //執行轉置 TEmsBasicInfo
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TSgwMaterial.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    #endregion

                    #region 第一次 Merge 經濟部(MOEA)
                    case "TFtyDecChemiFlow":
                        //執行轉置 TFtyDecChemiFlow (DataId：MOEA000001)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TFtyDecChemiFlow.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TChemicalMaterials":
                        //執行轉置 TChemicalMaterials (DataId：MOEA000002)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TChemicalMaterials.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TWhsleChemiInfo":
                        //執行轉置 TWhsleChemiInfo (DataId：MOEA000009)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TWhsleChemiInfo.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TBizExplosiveInfo":
                        //執行轉置 TBizExplosiveInfo (DataId：MOEA000011)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TBizExplosiveInfo.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TFactoryDanger":
                        //執行轉置 TFactoryDanger (DataId：MOEA000012)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TFactoryDanger.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    #endregion

                    #region 第一次 Merge 勞動部(MOL)
                    case "TNewChemiRegMgr":
                        //執行轉置 TNewChemiRegMgr (DataId：MOL000001)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TNewChemiRegMgr.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TPriorityChemicalMgr":
                        //執行轉置 TPriorityChemicalMgr (DataId：MOL000002)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TPriorityChemicalMgr.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TRegulatoryChemical":
                        //執行轉置 TRegulatoryChemical (DataId：MOL000003)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TRegulatoryChemical.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                     /*case "TDangerMachine":
                        //執行轉置 TRegulatoryChemical (DataId：MOL000004)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TDangerMachine.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;*/
                    #endregion

                    #region 第一次 Merge 衛福部(MOHW)
                    case "TFadenBook":
                        //執行轉置 TFadenBook (DataId：MOHW000004)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TFadenBook.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TMedicinalCertMgrInfo":
                        //執行轉置 TMedicinalCertMgrInfo (DataId：MOHW000002)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TMedicinalCertMgrInfo.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TAutoBorderMgr":
                        //執行轉置 TAutoBorderMgr (DataId：MOHW000001)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TAutoBorderMgr.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TTobaccoIngredient":
                        //執行轉置 TTobaccoIngredient (DataId：MOHW000003)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TTobaccoIngredient.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TCosmeticProduct":
                        //執行轉置 TCosmeticProduct (DataId：MOHW000005)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TCosmeticProduct.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TControlledDrugsMgr":
                        //執行轉置 TCosmeticProduct (DataId：MOHW000007)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TControlledDrugsMgr.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TFTraceBook":
                        //執行轉置 TCosmeticProduct (DataId：MOHW000008)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TFTraceBook.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TDTraceBook":
                        //執行轉置 TCosmeticProduct (DataId：MOHW000009)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TDTraceBook.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    #endregion

                    #region 第一次 Merge 農委會(COA)
                    case "TAnimalFeed":
                        //執行轉置 TAnimalFeed (DataId：COA000002)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TAnimalFeed.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TFertilizerMgrInfo":
                        //執行轉置 TFertilizerMgrInfo (DataId：COA000005)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TFertilizerMgrInfo.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TPesticideSafety":
                        //執行轉置 TPesticideSafety (DataId：COA000003)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TPesticideSafety.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TPesticideMgrInfo":
                        //執行轉置 TPesticideMgrInfo (DataId：COA000004)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TPesticideMgrInfo.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TVeterinaryDrugs":
                        //執行轉置 TVeterinaryDrugs (DataId：COA000001)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TVeterinaryDrugs.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    #endregion

                    #region 第一次 Merge 財政部(MOF)
                    case "TVisaLicenseAirImport":
                        //執行轉置 TVisaLicenseAirImport (DataId：MOF000001)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TVisaLicense.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TVisaLicenseSeaImport":
                        //執行轉置 TVisaLicenseSeaImport (DataId：MOF000002)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TVisaLicense.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TVisaLicenseAirExport":
                        //執行轉置 TVisaLicenseAirExport (DataId：MOF000005)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TVisaLicense.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TVisaLicenseSeaExport":
                        //執行轉置 TVisaLicenseSeaExport (DataId：MOF000006)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TVisaLicense.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TCigaretAlcoholMgr":
                        //執行轉置 TCigaretAlcoholMgr (DataId：MOF000007)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TCigaretAlcoholMgr.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    #endregion

                    #region 第一次 Merge 交通部(MOTC)
                    case "TDangerDeclare":
                        //執行轉置 TDangerDeclare (DataId：MOTC000001)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TDangerDeclare.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TDangerPass":
                        //執行轉置 TDangerPass (DataId：MOTC000002)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TDangerPass.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    #endregion

                    #region 第一次 Merge 內政部(MOI)
                    case "TFireSafety":
                        //執行轉置 TFireSafety (DataId：MOI000001)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TFireSafety.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    #endregion

                    #region 第一次 Merge 自來水(WATER)
                    case "TWaterPharmacyTpi":
                        //執行轉置 TWaterPharmacyTpi (DataId：TPI000001)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TWaterPharmacyTpi.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TWaterPharmacyTwc":
                        //執行轉置 TWaterPharmacyTwc (DataId：MOEA000010)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TWaterPharmacyTwc.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TWaterPharmacyKmn":
                        //執行轉置 TWaterPharmacyKmn (DataId：KMN000001)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TWaterPharmacyKmn.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TWaterPharmacyMsu":
                        //執行轉置 TWaterPharmacyMsu (DataId：MSU000001)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TWaterPharmacyMsu.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    #endregion

                    #region 第一次 Merge 消防局(FD)
                    case "TCommonDangerItemTYC":
                        //執行轉置 TFireSafety (DataId：TYFD000001)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TCommonDangerItemTYC.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TCommonDangerItemNTPC":
                        //執行轉置 TFireSafety (DataId：NTPC000001)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TCommonDangerItemNTPC.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TCommonDangerItemTPI":
                        //執行轉置 TFireSafety (DataId：TPI000002)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TCommonDangerItemTPI.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TCommonDangerItemTXG":
                        //執行轉置 TFireSafety (DataId：TXG000001)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TCommonDangerItemTXG.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    #endregion

                    #region 第一次 Merge 法務部(MOJ)
                    case "TProcuratorateDrug":
                        //執行轉置 TProcuratorateDrug (DataId：MOJ000001) in MetaData
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TProcuratorateDrug.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    #endregion

                    #region 第一次 Merge 科技部(MOST)
                    /*case "TOSHTCBasicInfo":
                        //執行轉置 TOSHTCBasicInfo (DataId：MOST000001)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TOSHTCBasicInfo.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TSIPAOld":
                        //執行轉置 TSIPA (DataId：MOST000006)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TSIPA.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;*/
                    case "TSTSP":
                        //執行轉置 TSTSP (DataId：MOST000005)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TSTSP.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TOSHTC":
                        //執行轉置 TOSHTC (DataId：MOST000001)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TOSHTC.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TSIPA":
                        //執行轉置 TSIPA (DataId：MOST000006)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TSIPA.convertFunc(strMergeStep);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    #endregion

                    #region 第一次 Merge 商工資料(ComFacBiz)
                    case "TCompany":
                        //執行轉置 TCompany (DataId：MOEA000013)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TCompany.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TCompanyItem":
                        //執行轉置 TCompanyItem (DataId：MOEA000014)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TCompanyItem.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TBranch":
                        //執行轉置 TBranch (DataId：MOEA000015)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TBranch.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TBusiness":
                        //執行轉置 TBusiness (DataId：MOEA000016)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TBusiness.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TFactory":
                        //執行轉置 TFactory (DataId：MOEA000017)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TFactory.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TFactoryMix":
                        //執行轉置 TFactoryMix (DataId：MOEA000018)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TFactoryMix.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TFactoryIllegal":
                        //執行轉置 TFactoryIllegal (DataId：MOEA000019)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TFactoryIllegal.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TLtdPartnership":
                        //執行轉置 TLtdPartnership (DataId：MOEA000020)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TLtdPartnership.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TLtdPartnershipItem":
                        //執行轉置 TLtdPartnershipItem (DataId：MOEA000021)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TLtdPartnershipItem.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    case "TLtdPartners":
                        //執行轉置 TLtdPartners (DataId：MOEA000022)
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 1);   //設成轉置中
                        TLtdPartners.convertFunc(strMergeStep, intPageDataCount);
                        funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //設成閒置
                        break;
                    #endregion

                    #region 第二次 Merge (AtLast)
                    case "1":
                        funcUtil.setIsIdle(strMergeClass, "1", 1);   //設成轉置中
                        #region 整併公司/工廠/營利事業基本資料
                        //只整併 經濟部三張大表 及 環保署列管汙染源基本資料。
                        //ComFacBizInfo.mergeEPAComFacBizInfo();
                        #endregion
                        funcUtil.setIsIdle(strMergeClass, "1", 0);   //設成閒置
                        break;
                    case "2":
                        funcUtil.setIsIdle(strMergeClass, "2", 1);   //設成轉置中
                        #region 整併化學物質基本資料
                        ChemicalData.mergeChemicalData();
                        #endregion
                        funcUtil.setIsIdle(strMergeClass, "2", 0);   //設成閒置
                        break;
                    case "3":
                        funcUtil.setIsIdle(strMergeClass, "3", 1);   //設成轉置中
                        #region 整併公司與化學物質對應表
                        ChemiComMapping.mergeChemiComMapping();
                        #endregion
                        funcUtil.setIsIdle(strMergeClass, "3", 0);   //設成閒置
                        break;
                    case "4":
                        funcUtil.setIsIdle(strMergeClass, "4", 1);   //設成轉置中
                        #region 整併運作資訊
                        SupplierCustomerInfo.mergeSupplierCustomerInfo();
                        #endregion
                        funcUtil.setIsIdle(strMergeClass, "4", 0);   //設成閒置
                        break;
                    case "5":
                        funcUtil.setIsIdle(strMergeClass, "5", 1);   //設成轉置中
                        #region 整併證件資料
                        ChemiCredential.mergeMainFunc();
                        #endregion
                        funcUtil.setIsIdle(strMergeClass, "5", 0);   //設成閒置
                        break;
                    case "6":
                        funcUtil.setIsIdle(strMergeClass, "6", 1);   //設成轉置中
                        #region 整併其他客製化案例
                        OtherCustomize.mergeOtherCustomize();
                        #endregion
                        funcUtil.setIsIdle(strMergeClass, "6", 0);   //設成閒置
                        break;
                    case "7":
                        funcUtil.setIsIdle(strMergeClass, "7", 1);   //設成轉置中
                        #region 整併化學產品基本資料
                        ProductData.mergeProductData();
                        #endregion
                        funcUtil.setIsIdle(strMergeClass, "7", 0);   //設成閒置
                        break;
                    #endregion

                    default:
                        break;

                }

            }
            catch (Exception ex)
            {
                funcUtil.setIsIdle(strMergeClass, strMergeStep, 0);   //將無預警出錯的資料集設成為閒置
                funcUtil.ins2ndDataTransErrorLog("0", "main_Error", "-", ex.Message, strMergeClass + " / " + strMergeStep);
                throw ex;
            }

            ////// 轉置結束 //////
            Console.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + " 完成執行資料轉置。");
            //Console.ReadLine();     //記得註解掉
        }
    }
}