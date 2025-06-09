using System;
using System.Collections;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Normalization_MS
{
    class funcUtil
    {
        public static string strBaseDBConn = ConfigurationManager.AppSettings["BaseDBConnectionStr"];           //ChemiBase DB 連限字串
        public static string strPrimaryDBConn = ConfigurationManager.AppSettings["PrimaryDBConnectionStr"];     //ChemiPrimary DB 連限字串
        public static string strTempDBConn = ConfigurationManager.AppSettings["TempDBConnectionStr"];           //ChemiTemp DB 連限字串

        #region 程式主架構函式設定
        /// <summary>
        /// 若同時執行的程式數超過三支，則不進行動作。
        /// </summary>
        /// <returns>True-是；False-否</returns>
        public static bool synRunPrgmCount()
        {
            bool blFlag = false;
            int IsIdleCount = 0;

            string strSQL = "SELECT count(*) AS IsIdleCount FROM MergeConfigSet WHERE IsIdle=1";

            try
            {
                //因為需要從 Base DB 取出轉置的資料，需要定義 Base DB 連線。
                SqlConnection ConnB = new SqlConnection(strBaseDBConn);
                SqlCommand CmdB = ConnB.CreateCommand();
                ConnB.Open();
                CmdB.CommandTimeout = 3000;
                CmdB.CommandText = strSQL;

                SqlDataReader drB = CmdB.ExecuteReader();
                if (drB.Read())
                {
                    IsIdleCount = drB.GetInt32(0);     //取出正在轉置的數量
                }
                if (drB != null)
                    drB.Close();

                ConnB.Dispose();
                if (ConnB != null)
                    ConnB.Close();
                CmdB.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "發生轉置異常，訊息：" + ex.Message);
            }

            if (IsIdleCount >= 2)
                blFlag = true;

            return blFlag;
        }

        /// <summary>
        /// 根據Parser的轉置紀錄，檢查今天需轉置的資料集共那些，將這些資料集的hasUnTransData設置成1
        /// </summary>
        public static void CheckParserDataSet()
        {
            try
            {
                //若有取出轉置資料，則需將該筆資料改變閒置狀態，以防止程式重複執行轉置。
                SqlConnection Conn = new SqlConnection(strBaseDBConn);
                SqlCommand Cmd = Conn.CreateCommand();
                Cmd.CommandTimeout = 3000;

                Conn.Open();
                bool isIdleXmlParser = true;
                string lastCheckParserDataTime = string.Empty;
                string nowDateTime = DateTime.Now.ToString("yyyy-MM-dd");
                string strStartTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                
                string currentDate = DateTime.Now.ToString("dd");
                int intDataCount = 0;

                ///取出上次Check時間
                Cmd.CommandText = "SELECT ConfigValue FROM Config WHERE ConfigName = 'LastCheckParserDataTime'";
                Cmd.Parameters.Clear();
                SqlDataReader drP = Cmd.ExecuteReader();

                if (drP.Read())
                {
                    lastCheckParserDataTime = drP["ConfigValue"].ToString();
                }
                if (drP != null)
                    drP.Close();

                ///遇到毒化物與非登的資料，可能會轉超過10點，所以多一層判斷
                Cmd.CommandText = "SELECT IsIdle FROM MergeConfigSet WHERE MergeStep = 'XmlParser'";
                Cmd.Parameters.Clear();
                drP = Cmd.ExecuteReader();

                if (drP.Read())
                {
                    var isIdle = drP["IsIdle"].ToString();

                    if (isIdle == "1")
                        isIdleXmlParser = false;
                }
                if (drP != null)
                    drP.Close();

                ///若XMLParser轉置太久，則可能在Parse非登或毒化物資料，則根據日期預先將hasUnTransData設置成1
                if (nowDateTime != lastCheckParserDataTime && DateTime.Now > Convert.ToDateTime(DateTime.Now.ToString("yyyy/MM/dd") + " 23:50:00") && DateTime.Now < Convert.ToDateTime(DateTime.Now.ToString("yyyy/MM/dd") + " 23:55:59") && isIdleXmlParser == false)
                {

                    #region -Fdenbook客製化設定 

                    string configValue = "";

                    string strCmd = @"SELECT ConfigValue FROM Config WHERE ConfigName = 'FdenbookCDXMappingConfig'";
                    Cmd.CommandText = strCmd;
                    Cmd.Parameters.Clear();
                    drP = Cmd.ExecuteReader();
                    if (drP.Read())
                    {
                        configValue = drP["ConfigValue"].ToString();
                    }
                    if (drP != null)
                        drP.Close();

                    string[] sp_Date = configValue.Split(',');

                    foreach (var date in sp_Date)
                    {
                        if (currentDate == date)
                        {
                            try
                            {
                                strCmd = @"UPDATE MergeConfigSet SET hasUnTransData = @hasUnTransData WHERE MergeStep = @MergeStep";
                                Cmd.CommandText = strCmd;
                                Cmd.Parameters.Clear();
                                Cmd.Parameters.AddWithValue("@MergeStep", "TFadenBook");
                                Cmd.Parameters.AddWithValue("@hasUnTransData", "1");
                                Cmd.ExecuteNonQuery();

                            }
                            catch (Exception ex)
                            {
                                funcUtil.ins2ndDataTransErrorLog("0", "funcUtil_Error", "-", ex.Message, "CheckParserDataSet：Fdenbook客製化處理");
                            }

                            break;
                        }
                    }

                    #endregion

                    #region CHEMIST_MAIN客製化設定

                    if(currentDate == "01")
                    {
                        try
                        {
                            strCmd = @"UPDATE MergeConfigSet SET hasUnTransData = @hasUnTransData WHERE MergeStep = @MergeStep";
                            Cmd.CommandText = strCmd;
                            Cmd.Parameters.Clear();
                            Cmd.Parameters.AddWithValue("@MergeStep", "CHEMIST_MAIN");
                            Cmd.Parameters.AddWithValue("@hasUnTransData", "1");
                            Cmd.ExecuteNonQuery();

                        }
                        catch (Exception ex)
                        {
                            funcUtil.ins2ndDataTransErrorLog("0", "funcUtil_Error", "-", ex.Message, "CheckParserDataSet：CHEMIST_MAIN客製化處理");
                        }
                    }

                    #endregion

                    #region TExistingChemical客製化設定

                    if (currentDate == "01")
                    {
                        try
                        {
                            strCmd = @"UPDATE MergeConfigSet SET hasUnTransData = @hasUnTransData WHERE MergeStep = @MergeStep";
                            Cmd.CommandText = strCmd;
                            Cmd.Parameters.Clear();
                            Cmd.Parameters.AddWithValue("@MergeStep", "TExistingChemical");
                            Cmd.Parameters.AddWithValue("@hasUnTransData", "1");
                            Cmd.ExecuteNonQuery();

                        }
                        catch (Exception ex)
                        {
                            funcUtil.ins2ndDataTransErrorLog("0", "funcUtil_Error", "-", ex.Message, "CheckParserDataSet：TExistingChemical客製化處理");
                        }
                    }

                    #endregion

                    #region TMedicinalCertMgrInfo客製化設定

                    if (currentDate == "05")
                    {
                        try
                        {
                            strCmd = @"UPDATE MergeConfigSet SET hasUnTransData = @hasUnTransData WHERE MergeStep = @MergeStep";
                            Cmd.CommandText = strCmd;
                            Cmd.Parameters.Clear();
                            Cmd.Parameters.AddWithValue("@MergeStep", "TMedicinalCertMgrInfo");
                            Cmd.Parameters.AddWithValue("@hasUnTransData", "1");
                            Cmd.ExecuteNonQuery();

                        }
                        catch (Exception ex)
                        {
                            funcUtil.ins2ndDataTransErrorLog("0", "funcUtil_Error", "-", ex.Message, "CheckParserDataSet：TMedicinalCertMgrInfo客製化處理");
                        }
                    }

                    #endregion

                    #region TToxChemiOperation & TToxChemiRestAmount客製化設定

                    if (currentDate == "20")
                    {
                        try
                        {
                            strCmd = @"UPDATE MergeConfigSet SET hasUnTransData = @hasUnTransData WHERE MergeStep = @MergeStep1 OR MergeStep = @MergeStep2";
                            Cmd.CommandText = strCmd;
                            Cmd.Parameters.Clear();
                            Cmd.Parameters.AddWithValue("@MergeStep1", "TToxChemiOperation");
                            Cmd.Parameters.AddWithValue("@MergeStep2", "TToxChemiRestAmount");
                            Cmd.Parameters.AddWithValue("@hasUnTransData", "1");
                            Cmd.ExecuteNonQuery();

                        }
                        catch (Exception ex)
                        {
                            funcUtil.ins2ndDataTransErrorLog("0", "funcUtil_Error", "-", ex.Message, "CheckParserDataSet：TToxChemiOperation & TToxChemiRestAmount客製化處理");
                        }
                    }

                    #endregion
                }

                //一天只轉置一次
                if (nowDateTime != lastCheckParserDataTime && isIdleXmlParser)
                {

                    ArrayList tempTableList = new ArrayList();

                    ///取出自上次確認的日期到今天，所有需要進行第一次轉置的TableName
                    Cmd.CommandText = @"SELECT DISTINCT T1.DataId, T2.TableName FROM
                                        (SELECT DISTINCT DataId, ImportFileOrTable FROM DataTransLog 
                                        WHERE StartTime >= @StartTime AND Phase = '1' AND DataId <> '_NONE_') T1
                                        LEFT JOIN TempDataSet T2 ON T1.DataId = T2.DataId
                                        LEFT JOIN MergeConfigSet T3 ON T2.TableName = T3.MergeStep
                                        WHERE TableName IS NOT NULL AND MergeStep IS NOT NULL ";
                    Cmd.Parameters.Clear();
                    Cmd.Parameters.AddWithValue("@StartTime", lastCheckParserDataTime + " 20:00:00");
                    drP = Cmd.ExecuteReader();

                    while (drP.Read())
                    {
                        var tableName = drP["TableName"].ToString();

                        tempTableList.Add(tableName);
                    }
                    if (drP != null)
                        drP.Close();


                    ///因CHEMIST_MAIN為特殊處理，所以並沒辦法撈到此表，需要客製化於每月01日，加入轉置。
                    if (currentDate == "01")
                    {
                        tempTableList.Add("CHEMIST_MAIN");
                    }

                    ///逐筆將hasUnTransData設置成1
                    foreach (var tableName in tempTableList)
                    {
                        string strCmd = @"UPDATE MergeConfigSet SET hasUnTransData = @hasUnTransData WHERE MergeStep = @MergeStep";
                        Cmd.CommandText = strCmd;
                        Cmd.Parameters.Clear();
                        Cmd.Parameters.AddWithValue("@MergeStep", tableName);
                        Cmd.Parameters.AddWithValue("@hasUnTransData", "1");
                        Cmd.ExecuteNonQuery();

                        intDataCount++;
                    }

                    ///最後將FdenbookCDXMappingConfig的值更新為當天的日期
                    try
                    {
                        string strCmd = @"UPDATE Config SET ConfigValue = @ConfigValue WHERE ConfigName = 'LastCheckParserDataTime'";
                        Cmd.CommandText = strCmd;
                        Cmd.Parameters.Clear();
                        Cmd.Parameters.AddWithValue("@ConfigValue", nowDateTime);
                        Cmd.ExecuteNonQuery();

                    }
                    catch (Exception ex)
                    {
                        funcUtil.ins2ndDataTransErrorLog("0", "funcUtil_Error", "-", ex.Message, "CheckParserDataSet：Config中FdenbookCDXMappingConfig更新");
                    }

                    string strEndTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); //取得轉置開始時間
                    funcUtil.ins1stDataTransLog("", "",
                                                 strStartTime, strEndTime, "CheckParserDataSet",
                                                 intDataCount, 0);
                }

                Conn.Dispose();
                if (Conn != null)
                    Conn.Close();
                Cmd.Dispose();
            }
            catch (Exception ex)
            {
                //Do Nothing
                funcUtil.ins2ndDataTransErrorLog("0", "funcUtil_Error", "-", ex.Message, "CheckParserDataSet：");
            }
        }
        
        /// <summary>
        /// 取得當前整併類別及當前整併階段
        /// </summary>
        /// <param name="strMergeClass">當前整併類別</param>
        /// <param name="strMergeStep">當前整併階段</param>
        public static void getMergeClassAndStep(ref string strMergeClass, ref string strMergeStep)
        {
            /// strGetMergeStepSQL 邏輯說明：
            /// 由 共通資料表(ChemicalBase) 的 正規化整併排程設定(MergeConfigSet) 取得欲執行的階段名稱，
            /// 但需要錯開 正規化整併互斥設定(MergeMutexSet) 中的互斥群組設定與 正規化整併類別互斥設定(MergeClassMutexSet)。
            /// 各轉置階段 1stMerge 及 2ndMerge 需要錯開執行，
            /// 每次取得執行的階段，其 LastMergeTime 為最舊的。
            /// 語法如下：
            string strGetMergeStepSQL =
                "SELECT BaseTB.MergeClass, BaseTB.MergeStep " +
                " FROM MergeConfigSet AS BaseTB" +
                " LEFT JOIN (" +
                " SELECT *" +
                " FROM (" +
                " SELECT DISTINCT c.MergeClass, c.MergeStep" +
                " FROM (" +
                " SELECT a.MergeClass, a.MergeStep, b.MergeGroup" +
                " FROM MergeConfigSet AS a" +
                " LEFT JOIN MergeMutexSet AS b ON a.MergeClass = b.MergeClass AND a.MergeStep = b.MergeStep" +
                " WHERE a.IsIdle = 1 AND a.IsAbolish = 1) AS T1" +
                " LEFT JOIN MergeMutexSet AS c ON T1.MergeClass = c.MergeClass AND T1.MergeGroup = c.MergeGroup" +
                " WHERE T1.MergeGroup IS NOT NULL) AS T2" +
                " UNION " +
                " SELECT g.MergeClass, g.MergeStep" +
                " FROM (" +
                " SELECT DISTINCT f.MergeClass" +
                " FROM (" +
                " SELECT d.MergeClass, e.ClassMutexGroup" +
                " FROM MergeConfigSet AS d" +
                " LEFT JOIN MergeClassMutexSet AS e ON d.MergeClass = e.MergeClass" +
                " WHERE d.IsIdle = 1 AND d.IsAbolish = 1) AS T3" +
                " LEFT JOIN MergeClassMutexSet AS f ON T3.ClassMutexGroup = f.ClassMutexGroup AND T3.MergeClass <> f.MergeClass) AS T4" +
                " LEFT JOIN MergeConfigSet AS g ON T4.MergeClass = g.MergeClass) AS CntDoClass" +
                " ON BaseTB.MergeClass = CntDoClass.MergeClass AND BaseTB.MergeStep = CntDoClass.MergeStep" +
                " WHERE CntDoClass.MergeClass IS NULL AND BaseTB.IsIdle = 0 AND BaseTB.IsAbolish = 1 AND BaseTB.hasUnTransData = 1 " +
                " ORDER BY BaseTB.MergeClass Desc,BaseTB.LastMergeTime;";

            try
            {
                //因為需要從 Base DB 取出轉置的資料，需要定義 Base DB 連線。
                SqlConnection ConnB = new SqlConnection(strBaseDBConn);
                SqlCommand CmdB = ConnB.CreateCommand();
                ConnB.Open();
                CmdB.CommandTimeout = 3000;
                CmdB.CommandText = strGetMergeStepSQL;

                SqlDataReader drB = CmdB.ExecuteReader();
                if (drB.Read())
                {
                    strMergeClass = drB["MergeClass"].ToString().Trim();     //取出欲轉置的 當前整併類別
                    strMergeStep = drB["MergeStep"].ToString().Trim();      //取出欲轉置的 當前整併階段
                }
                if (drB != null)
                    drB.Close();

                ConnB.Dispose();
                if (ConnB != null)
                    ConnB.Close();
                CmdB.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "發生轉置異常，訊息：" + ex.Message);
            }
        }

        /// <summary>
        /// 設定執行整併階段的閒置狀態
        /// </summary>
        /// <param name="strMergeClass">整併類別(AtLast-最後正規化/MOEA-經濟部/EPA-環保署...以此類推)</param>
        /// <param name="strMergeStep">整併階段或來源資料表格名稱(Temp DB Table Name)</param>
        /// <param name="intIdle">閒置狀態(0-閒置；1-轉置中)</param>
        public static void setIsIdle(string strMergeClass, string strMergeStep, int intIdle)
        {
            try
            {
                //若有取出轉置資料，則需將該筆資料改變閒置狀態，以防止程式重複執行轉置。
                SqlConnection Conn = new SqlConnection(strBaseDBConn);
                SqlCommand Cmd = Conn.CreateCommand();
                Cmd.CommandTimeout = 3000;

                //因為需要從 Temp DB 取出轉置的資料，需要定義 Temp DB 連線。
                SqlConnection ConnT = new SqlConnection(strTempDBConn);
                SqlCommand CmdT = ConnT.CreateCommand();
                CmdT.CommandTimeout = 3000;

                Conn.Open();
                ConnT.Open();

                switch (intIdle)
                {
                    //轉置中，依照原邏輯設置閒置狀態
                    case 1:
                        
                        Cmd.CommandText = "UPDATE MergeConfigSet SET IsIdle=@IsIdle, LastMergeTime=@LastMergeTime WHERE MergeClass=@MergeClass AND MergeStep=@MergeStep";
                        Cmd.Parameters.Clear();
                        Cmd.Parameters.AddWithValue("@IsIdle", intIdle);
                        Cmd.Parameters.AddWithValue("@LastMergeTime", DateTime.Now);
                        Cmd.Parameters.AddWithValue("@MergeClass", strMergeClass);
                        Cmd.Parameters.AddWithValue("@MergeStep", strMergeStep);
                        Cmd.ExecuteNonQuery();
                        break;

                    //資料集轉置完，若為第二次轉置，則依照原邏輯，若為第一次轉置，則先判斷
                    case 0:
                        if (strMergeClass == "TEMP")
                        {

                            Cmd.CommandText = "UPDATE MergeConfigSet SET LastMergeTime=@LastMergeTime WHERE MergeClass=@MergeClass AND MergeStep=@MergeStep";
                            Cmd.Parameters.Clear();
                            Cmd.Parameters.AddWithValue("@LastMergeTime", DateTime.Now);
                            Cmd.Parameters.AddWithValue("@MergeClass", strMergeClass);
                            Cmd.Parameters.AddWithValue("@MergeStep", strMergeStep);
                            Cmd.ExecuteNonQuery();
                        }
                        else if (strMergeClass == "AtLast")
                        {
                            var hasUnTransData = "0";
                            switch (strMergeStep)
                            {
                                //化學物質
                                case "2":
                                    string[] ChemicalDataSet = { "MOHWChemicalData" };
                                    break;

                                //運作資訊
                                case "4":
                                    string[] SupplierCustomerInfoSet = { "	EPASupplierCustomerInfo_Tox" };

                                    Cmd.CommandText = "SELECT ConfigValue FROM Config WHERE ConfigName = 'TTox_2nd_HasNonTransData'";
                                    Cmd.Parameters.Clear();
                                    SqlDataReader drP = Cmd.ExecuteReader();

                                    if (drP.Read())
                                    {
                                        var ConfigValue = drP["ConfigValue"].ToString();

                                        if (ConfigValue == "1")
                                            hasUnTransData = ConfigValue;
                                    }
                                    if (drP != null)
                                        drP.Close();
                                    break;

                                default:
                                    break;
                            }

                            Cmd.CommandText = "UPDATE MergeConfigSet SET IsIdle=@IsIdle, LastMergeTime=@LastMergeTime, hasUnTransData=@hasUnTransData WHERE MergeClass=@MergeClass AND MergeStep=@MergeStep";
                            Cmd.Parameters.Clear();
                            Cmd.Parameters.AddWithValue("@IsIdle", intIdle);
                            Cmd.Parameters.AddWithValue("@LastMergeTime", DateTime.Now);
                            Cmd.Parameters.AddWithValue("@hasUnTransData", hasUnTransData);
                            Cmd.Parameters.AddWithValue("@MergeClass", strMergeClass);
                            Cmd.Parameters.AddWithValue("@MergeStep", strMergeStep);
                            Cmd.ExecuteNonQuery();
                        }
                        else
                        {
                            var hasUnTransData = "0";

                            ///食品追溯追蹤資料需同時判別新舊版的資料表
                            if (strMergeStep == "TFTraceBook")
                            {
                                CmdT.CommandText = "SELECT TOP 1 TransId FROM( ";
                                CmdT.CommandText += "SELECT TOP 1 TransId FROM TFTraceBook WHERE IsChecked=1 AND IsConverted=0 GROUP BY TransId UNION ";
                                CmdT.CommandText += "SELECT TOP 1 TransId FROM TFTraceBookCpInfo WHERE IsChecked=1 AND IsConverted=0 GROUP BY TransId UNION ";
                                CmdT.CommandText += "SELECT TOP 1 TransId FROM TFTraceBookFaInfo WHERE IsChecked=1 AND IsConverted=0 GROUP BY TransId UNION ";
                                CmdT.CommandText += "SELECT TOP 1 TransId FROM TFTraceBookPdInfo WHERE IsChecked=1 AND IsConverted=0 GROUP BY TransId UNION ";
                                CmdT.CommandText += "SELECT TOP 1 TransId FROM TFTraceBookMaInfo WHERE IsChecked=1 AND IsConverted=0 GROUP BY TransId UNION ";
                                CmdT.CommandText += "SELECT TOP 1 TransId FROM TFTraceBookMfInfo WHERE IsChecked=1 AND IsConverted=0 GROUP BY TransId UNION ";
                                CmdT.CommandText += "SELECT TOP 1 TransId FROM TFTraceBookDvInfo WHERE IsChecked=1 AND IsConverted=0 GROUP BY TransId ";
                                CmdT.CommandText += ") AS T ";
                            }
                            else
                            {
                                CmdT.CommandText = "SELECT TOP 1 TransId FROM " + strMergeStep;

                                ///特殊處理，如果為CHEMIST_MAIN，則去除IsChecked欄位
                                if (strMergeStep == "CHEMIST_MAIN")
                                {
                                    CmdT.CommandText += " WHERE IsConverted=0x00";
                                }
                                else
                                {
                                    CmdT.CommandText += " WHERE IsChecked=1 AND IsConverted=0x00";
                                }
                            }

                            CmdT.Parameters.Clear();
                            SqlDataReader drT = CmdT.ExecuteReader();

                            if (drT.Read())
                            {
                                hasUnTransData = "1";
                            }
                            if (drT != null)
                                drT.Close();


                            Cmd.CommandText = "UPDATE MergeConfigSet SET IsIdle=@IsIdle, LastMergeTime=@LastMergeTime, hasUnTransData=@hasUnTransData WHERE MergeClass=@MergeClass AND MergeStep=@MergeStep";
                            Cmd.Parameters.Clear();
                            Cmd.Parameters.AddWithValue("@IsIdle", intIdle);
                            Cmd.Parameters.AddWithValue("@LastMergeTime", DateTime.Now);
                            Cmd.Parameters.AddWithValue("@hasUnTransData", hasUnTransData);
                            Cmd.Parameters.AddWithValue("@MergeClass", strMergeClass);
                            Cmd.Parameters.AddWithValue("@MergeStep", strMergeStep);
                            Cmd.ExecuteNonQuery();

                            Cmd.CommandText = "UPDATE MergeConfigSet SET hasUnTransData='1' WHERE MergeClass='AtLast' AND IsAbolish = 1 ";
                            Cmd.Parameters.Clear();
                            Cmd.ExecuteNonQuery();
                        }
                        break;
                    default:
                        break;
                }

                Conn.Dispose();
                if (Conn != null)
                    Conn.Close();
                Cmd.Dispose();

                //關閉 Temp DB 連線。
                ConnT.Dispose();
                if (ConnT != null)
                    ConnT.Close();
                CmdT.Dispose();
            }
            catch (Exception ex)
            {
                //Do Nothing
                funcUtil.ins2ndDataTransErrorLog("0", "funcUtil_Error", "-", ex.Message,"setIsIdle：" + strMergeClass + " / " + strMergeStep);
            }
        }
        #endregion
        
        #region 第一次Merge
        /// <summary>
        /// 將暫存資料表的轉置狀態設置為已轉置。
        /// </summary>
        /// <param name="strTransId">交易編號</param>
        /// <param name="strTempTableName">暫存資料表名稱</param>
        public static void updIsConvertedTrue(string strTransId, string strTempTableName)
        {
            try
            {
                SqlConnection Conn = new SqlConnection(strTempDBConn);
                SqlCommand Cmd = Conn.CreateCommand();
                Conn.Open();
                Cmd.CommandTimeout = 3000;
                Cmd.CommandText = "UPDATE " + strTempTableName + " SET IsConverted=1 WHERE TransId=@TransId";
                Cmd.Parameters.Clear();
                Cmd.Parameters.AddWithValue("@TransId", strTransId);
                Cmd.ExecuteNonQuery();

                Conn.Dispose();
                if (Conn != null)
                    Conn.Close();
                Cmd.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        /// <summary>
        /// 插入資料轉置異常記錄-單筆(第一次Merge)
        /// </summary>
        /// <param name="strNo">訊息代碼，TEMP DB的表格中的No欄位，記第幾筆。</param>
        /// <param name="strTransId">交易代碼(DataID+yyMMddHHmmssfff)</param>
        /// <param name="strTempTableName">暫存表格名稱</param>
        /// <param name="strErrorMessage">錯誤訊息</param>
        /// <param name="strComment">備註說明</param>
        public static void ins1stDataTransErrorLog(string strNo, string strTransId,
                                                string strTempTableName, string strErrorMessage, string strComment)
        {
            try
            {
                SqlConnection Conn = new SqlConnection(strBaseDBConn);
                SqlCommand Cmd = Conn.CreateCommand();
                Conn.Open();
                Cmd.CommandTimeout = 3000;
                Cmd.CommandText = "INSERT INTO DataTransErrorLog (No, Phase, TransId, TempTableName, ErrorMessage, UpdateTime, Comment)";
                Cmd.CommandText += " VALUES (@No, @Phase, @TransId, @TempTableName, @ErrorMessage, @UpdateTime, @Comment)";
                Cmd.Parameters.Clear();
                Cmd.Parameters.AddWithValue("@No", strNo);
                Cmd.Parameters.AddWithValue("@Phase", "2");
                Cmd.Parameters.AddWithValue("@TransId", strTransId);
                Cmd.Parameters.AddWithValue("@TempTableName", strTempTableName);
                Cmd.Parameters.AddWithValue("@ErrorMessage", strErrorMessage);
                Cmd.Parameters.AddWithValue("@UpdateTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Cmd.Parameters.AddWithValue("@Comment", strComment);
                Cmd.ExecuteNonQuery();

                Conn.Dispose();
                if (Conn != null)
                    Conn.Close();
                Cmd.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        /// <summary>
        /// 插入資料轉置記錄-整批(第一次Merge)
        /// </summary>
        /// <param name="strTransId">交易編號(DataId+yyMMddHHmmssfff)</param>
        /// <param name="strDataId">轉入資料集編號</param>
        /// <param name="strStartTime">轉入起始時間</param>
        /// <param name="strEndTime">轉入結束時間</param>
        /// <param name="strImportFileOrTable">轉入檔案或Table名稱(存到Primary DB的Table名稱)</param>
        /// <param name="intTotalCount">資料總筆數</param>
        /// <param name="intErrorCount">錯誤筆數</param>
        public static void ins1stDataTransLog(string strTransId, string strDataId,
                                           string strStartTime, string strEndTime, string strImportFileOrTable,
                                           int intTotalCount, int intErrorCount)
        {
            //若轉置筆數超過 0 筆再插入Log。
            if (intTotalCount > 0)
            {
                try
                {
                    SqlConnection Conn = new SqlConnection(strBaseDBConn);
                    SqlCommand Cmd = Conn.CreateCommand();
                    Conn.Open();
                    Cmd.CommandTimeout = 3000;
                    Cmd.CommandText = "INSERT INTO DataTransLog (TransId, Phase, DataId, StartTime, EndTime, ImportFileOrTable, TotalCount, ErrorCount)";
                    Cmd.CommandText += " VALUES (@TransId, @Phase, @DataId, @StartTime, @EndTime, @ImportFileOrTable, @TotalCount, @ErrorCount)";
                    Cmd.Parameters.Clear();
                    Cmd.Parameters.AddWithValue("@TransId", strTransId);
                    Cmd.Parameters.AddWithValue("@Phase", "2");
                    Cmd.Parameters.AddWithValue("@DataId", strDataId);
                    Cmd.Parameters.AddWithValue("@StartTime", strStartTime);
                    Cmd.Parameters.AddWithValue("@EndTime", strEndTime);
                    Cmd.Parameters.AddWithValue("@ImportFileOrTable", strImportFileOrTable);
                    Cmd.Parameters.AddWithValue("@TotalCount", intTotalCount);
                    Cmd.Parameters.AddWithValue("@ErrorCount", intErrorCount);
                    Cmd.ExecuteNonQuery();

                    Conn.Dispose();
                    if (Conn != null)
                        Conn.Close();
                    Cmd.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        #region 化學物質資料對應表轉置
        /// <summary>
        /// 化學物質資料對應表轉置
        /// </summary>
        /// <param name="strChemicalEngName">化學物質英文名稱</param>
        /// <param name="strChemicalChnName">化學物質中文名稱</param>
        /// <param name="strTransId">交易編號(TransId)</param>
        /// <param name="strTableName">來源資料表名稱</param>
        /// <param name="strNo">流水號(No)</param>
        public static void ChemicalDataMapping_Convert(ref string strChemicalEngName, ref string strChemicalChnName, string strTransId, string strTableName, string strNo)
        {
            #region 傳入參數去除空白字元
            if (string.IsNullOrEmpty(strChemicalEngName))
                strChemicalEngName = "-";
            else
                strChemicalEngName = strChemicalEngName.Trim();

            if (string.IsNullOrEmpty(strChemicalChnName))
                strChemicalChnName = "-";
            else
                strChemicalChnName = strChemicalChnName.Trim();
            #endregion

            if (strChemicalEngName.Length > 100 || strChemicalChnName.Length > 160)
            {
                try
                {
                    SqlConnection ConnP = new SqlConnection(strPrimaryDBConn);
                    SqlCommand CmdP = ConnP.CreateCommand();
                    ConnP.Open();
                    CmdP.CommandTimeout = 3000;

                    string strChemiFullEngName = strChemicalEngName; //完整英文名稱
                    string strChemiFullChnName = strChemicalChnName; // 完整中文名稱
                    if (strChemicalEngName.Length > 100)
                    {
                        strChemicalEngName = strChemicalEngName.Substring(strChemicalEngName.Length - 100, 100); //截短後英文名稱
                    }
                    if (strChemicalChnName.Length > 160)
                    {
                        strChemicalChnName = strChemicalChnName.Substring(strChemicalChnName.Length - 160, 160); //截短後中文名稱
                    }

                    //檢查是否存在，不存在則insert。
                    CmdP.CommandText = "SELECT * FROM ChemicalDataMapping ";
                    CmdP.CommandText += "WHERE ChemicalEngName=@EngName AND ChemicalChnName=@ChnName ";
                    CmdP.Parameters.Clear();
                    CmdP.Parameters.AddWithValue("@EngName", strChemicalEngName);
                    CmdP.Parameters.AddWithValue("@ChnName", strChemicalChnName);

                    SqlDataReader drTData = CmdP.ExecuteReader();
                    if (!drTData.HasRows)
                    {
                        drTData.Close();
                        CmdP.CommandText = "INSERT INTO ChemicalDataMapping(ChemicalEngName, ChemicalChnName, ";
                        CmdP.CommandText += "ChemiFullEngName, ChemiFullChnName, UpdateDate, TransId, PrimaryTableName, IsConverted, No) ";
                        CmdP.CommandText += "VALUES (@ChemicalEngName, @ChemicalChnName, @ChemiFullEngName, ";
                        CmdP.CommandText += "@ChemiFullChnName, @UpdateDate, @TransId, @PrimaryTableName, @IsConverted, @No) ";
                        CmdP.Parameters.Clear();
                        CmdP.Parameters.AddWithValue("@ChemicalEngName", strChemicalEngName);
                        CmdP.Parameters.AddWithValue("@ChemicalChnName", strChemicalChnName);
                        CmdP.Parameters.AddWithValue("@ChemiFullEngName", strChemiFullEngName);
                        CmdP.Parameters.AddWithValue("@ChemiFullChnName", strChemiFullChnName);
                        CmdP.Parameters.AddWithValue("@UpdateDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        CmdP.Parameters.AddWithValue("@TransId", strTransId);
                        CmdP.Parameters.AddWithValue("@PrimaryTableName", strTableName);
                        CmdP.Parameters.AddWithValue("@IsConverted", "0");
                        CmdP.Parameters.AddWithValue("@No", strNo);
                        CmdP.ExecuteNonQuery();
                    }
                    else
                    {
                        drTData.Close();
                    }

                    ConnP.Dispose();
                    if (ConnP != null)
                        ConnP.Close();
                    CmdP.Dispose();
                }
                catch (Exception ex)
                {
                    funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strTableName, ex.Message, "化學物質資料對應表轉置失敗");
                }
            }
        }
        #endregion

        #region 化學產品資料對應表轉置
        /// <summary>
        /// 化學產品資料對應表轉置
        /// </summary>
        /// <param name="strChemicalEngName">產品英文名稱</param>
        /// <param name="strChemicalChnName">產品中文名稱</param>
        /// <param name="strTransId">交易編號(TransId)</param>
        /// <param name="strTableName">來源資料表名稱</param>
        /// <param name="strNo">流水號(No)</param>
        public static void ProductMapping_Convert(ref string strProductEngName, ref string strProductChnName, string strTransId, string strTableName, string strNo)
        {
            #region 傳入參數去除空白字元
            if (string.IsNullOrEmpty(strProductEngName))
                strProductEngName = "-";
            else
                strProductEngName = strProductEngName.Trim();

            if (string.IsNullOrEmpty(strProductChnName))
                strProductChnName = "-";
            else
                strProductChnName = strProductChnName.Trim();
            #endregion

            if (strProductEngName.Length > 100 || strProductChnName.Length > 100)
            {
                try
                {
                    SqlConnection ConnP = new SqlConnection(strPrimaryDBConn);
                    SqlCommand CmdP = ConnP.CreateCommand();
                    ConnP.Open();
                    CmdP.CommandTimeout = 3000;

                    string strProductFullEngName = strProductEngName; //完整產品英文名稱
                    string strProductFullChnName = strProductChnName; // 完整產品中文名稱
                    if (strProductEngName.Length > 100)
                    {
                        strProductEngName = strProductEngName.Substring(strProductEngName.Length - 100, 100); //截短後產品英文名稱
                    }
                    if (strProductChnName.Length > 100)
                    {
                        strProductChnName = strProductChnName.Substring(strProductChnName.Length - 100, 100); //截短後產品中文名稱
                    }

                    //檢查是否存在，不存在則insert。
                    CmdP.CommandText = "SELECT * FROM ProductMapping ";
                    CmdP.CommandText += "WHERE ProductEngName=@EngName AND ProductChnName=@ChnName ";
                    CmdP.Parameters.Clear();
                    CmdP.Parameters.AddWithValue("@EngName", strProductEngName);
                    CmdP.Parameters.AddWithValue("@ChnName", strProductChnName);

                    SqlDataReader drTData = CmdP.ExecuteReader();
                    if (!drTData.HasRows)
                    {
                        drTData.Close();
                        CmdP.CommandText = "INSERT INTO ProductMapping(ProductEngName, ProductChnName, ";
                        CmdP.CommandText += "ProductFullEngName, ProductFullChnName, UpdateDate, TransId, PrimaryTableName, IsConverted, No) ";
                        CmdP.CommandText += "VALUES (@ProductEngName, @ProductChnName, @ProductFullEngName, ";
                        CmdP.CommandText += "@ProductFullChnName, @UpdateDate, @TransId, @PrimaryTableName, @IsConverted, @No) ";
                        CmdP.Parameters.Clear();
                        CmdP.Parameters.AddWithValue("@ProductEngName", strProductEngName);
                        CmdP.Parameters.AddWithValue("@ProductChnName", strProductChnName);
                        CmdP.Parameters.AddWithValue("@ProductFullEngName", strProductFullEngName);
                        CmdP.Parameters.AddWithValue("@ProductFullChnName", strProductFullChnName);
                        CmdP.Parameters.AddWithValue("@UpdateDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        CmdP.Parameters.AddWithValue("@TransId", strTransId);
                        CmdP.Parameters.AddWithValue("@PrimaryTableName", strTableName);
                        CmdP.Parameters.AddWithValue("@IsConverted", "0");
                        CmdP.Parameters.AddWithValue("@No", strNo);
                        CmdP.ExecuteNonQuery();
                    }
                    else
                    {
                        drTData.Close();
                    }

                    ConnP.Dispose();
                    if (ConnP != null)
                        ConnP.Close();
                    CmdP.Dispose();
                }
                catch (Exception ex)
                {
                    funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strTableName, ex.Message, "化學產品資料對應表轉置失敗");
                }
            }
        }
        #endregion

        #region 來源資料PK對應表轉置
        /// <summary>
        /// 來源資料PK對應表轉置
        /// </summary>
        /// <param name="strOriginalPK">來源資料的PK</param>
        /// <param name="strTableName">來源資料表名稱</param>
        /// <param name="strTransId">交易編號(TransId)</param>
        /// <param name="strNo">流水號(No)</param>
        public static string OriginalMapping_Convert(string strOriginalPK, string strTableName, string strTransId, string strNo)
        {
            string OriginalSN = "-";

            #region 傳入參數去除空白字元
            if (string.IsNullOrEmpty(strOriginalPK))
                strOriginalPK = "-";
            else
                strOriginalPK = strOriginalPK.Trim();
            #endregion

            try
            {
                SqlConnection ConnP = new SqlConnection(strPrimaryDBConn);
                SqlCommand CmdP = ConnP.CreateCommand();
                ConnP.Open();
                CmdP.CommandTimeout = 3000;
                //檢查是否存在，不存在則insert。
                CmdP.CommandText = "SELECT * FROM OriginalMapping ";
                CmdP.CommandText += "WHERE OriginalPK=@OriginalPK AND TempTableName=@TempTableName ";
                CmdP.Parameters.Clear();
                CmdP.Parameters.AddWithValue("@OriginalPK", strOriginalPK);
                CmdP.Parameters.AddWithValue("@TempTableName", strTableName);

                SqlDataReader drTData = CmdP.ExecuteReader();
                if (!drTData.HasRows)
                {
                    drTData.Close();
                    CmdP.CommandText = "INSERT INTO OriginalMapping(OriginalPK, TempTableName, ";
                    CmdP.CommandText += "TransId, No, UpdateDate) ";
                    CmdP.CommandText += "VALUES (@OriginalPK, @TempTableName, @TransId, ";
                    CmdP.CommandText += "@No, @UpdateDate);SELECT @@IDENTITY; ";
                    CmdP.Parameters.Clear();
                    CmdP.Parameters.AddWithValue("@OriginalPK", strOriginalPK);
                    CmdP.Parameters.AddWithValue("@TempTableName", strTableName);
                    CmdP.Parameters.AddWithValue("@TransId", strTransId);
                    CmdP.Parameters.AddWithValue("@No", strNo);
                    CmdP.Parameters.AddWithValue("@UpdateDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    //CmdP.ExecuteNonQuery();
                    OriginalSN = CmdP.ExecuteScalar().ToString();
                }
                else
                {
                    drTData.Read();
                    OriginalSN = drTData["OriginalSN"].ToString();
                    drTData.Close();
                }

                ConnP.Dispose();
                if (ConnP != null)
                    ConnP.Close();
                CmdP.Dispose();
            }
            catch (Exception ex)
            {
                funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strTableName, ex.Message, "來源資料PK對應表轉置失敗");
            }
            return OriginalSN;
        }
        #endregion

        #endregion

        #region 第二次Merge
        /// <summary>
        /// 插入資料轉置異常記錄-單筆(第二次Merge)
        /// </summary>
        /// <param name="strNo">訊息代碼，TEMP DB的表格中的No欄位，記第幾筆。</param>
        /// <param name="strTransId">交易代碼(DataID+yyMMddHHmmssfff)</param>
        /// <param name="strTempTableName">暫存表格名稱</param>
        /// <param name="strErrorMessage">錯誤訊息</param>
        /// <param name="strComment">備註說明</param>
        public static void ins2ndDataTransErrorLog(string strNo, string strTransId,
                                                string strPrimaryTableName, string strErrorMessage, string strComment)
        {
            try
            {
                SqlConnection Conn = new SqlConnection(strBaseDBConn);
                SqlCommand Cmd = Conn.CreateCommand();
                Conn.Open();
                Cmd.CommandTimeout = 3000;
                Cmd.CommandText = "INSERT INTO DataTransErrorLog (No, Phase, TransId, TempTableName, ErrorMessage, UpdateTime, Comment)";
                Cmd.CommandText += " VALUES (@No, @Phase, @TransId, @TempTableName, @ErrorMessage, @UpdateTime, @Comment)";
                Cmd.Parameters.Clear();
                Cmd.Parameters.AddWithValue("@No", strNo);
                Cmd.Parameters.AddWithValue("@Phase", "3");
                Cmd.Parameters.AddWithValue("@TransId", strTransId);
                Cmd.Parameters.AddWithValue("@TempTableName", strPrimaryTableName);
                Cmd.Parameters.AddWithValue("@ErrorMessage", strErrorMessage);
                Cmd.Parameters.AddWithValue("@UpdateTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Cmd.Parameters.AddWithValue("@Comment", strComment);
                Cmd.ExecuteNonQuery();

                Conn.Dispose();
                if (Conn != null)
                    Conn.Close();
                Cmd.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        /// <summary>
        /// 插入資料轉置記錄-整批(第二次Merge)
        /// </summary>
        /// <param name="strStartTime">轉入起始時間</param>
        /// <param name="strEndTime">轉入結束時間</param>
        /// <param name="strImportFileOrTable">轉置的Table名稱(若多筆以逗號,隔開)</param>
        /// <param name="intTotalCount">資料總筆數</param>
        /// <param name="intErrorCount">錯誤筆數</param>
        public static void ins2ndDataTransLog(string strStartTime, string strEndTime, string strImportFileOrTable,
                                           int intTotalCount, int intErrorCount)
        {
            if (intTotalCount > 0)
            {
                try
                {
                    SqlConnection Conn = new SqlConnection(strBaseDBConn);
                    SqlCommand Cmd = Conn.CreateCommand();
                    Conn.Open();
                    Cmd.CommandTimeout = 3000;
                    Cmd.CommandText = "INSERT INTO DataTransLog (TransId, Phase, DataId, StartTime, EndTime, ImportFileOrTable, TotalCount, ErrorCount)";
                    Cmd.CommandText += " VALUES (@TransId, @Phase, @DataId, @StartTime, @EndTime, @ImportFileOrTable, @TotalCount, @ErrorCount)";
                    Cmd.Parameters.Clear();
                    Cmd.Parameters.AddWithValue("@TransId", "2ndAP" + DateTime.Now.ToString("yyyyMMddHHmmssfff"));
                    Cmd.Parameters.AddWithValue("@Phase", "3");
                    Cmd.Parameters.AddWithValue("@DataId", "-");
                    Cmd.Parameters.AddWithValue("@StartTime", strStartTime);
                    Cmd.Parameters.AddWithValue("@EndTime", strEndTime);
                    Cmd.Parameters.AddWithValue("@ImportFileOrTable", strImportFileOrTable);
                    Cmd.Parameters.AddWithValue("@TotalCount", intTotalCount);
                    Cmd.Parameters.AddWithValue("@ErrorCount", intErrorCount);
                    Cmd.ExecuteNonQuery();

                    Conn.Dispose();
                    if (Conn != null)
                        Conn.Close();
                    Cmd.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        /// <summary>
        /// 將正規化資料表的合併狀態設置為已合併。
        /// </summary>
        /// <param name="strTableName">資料表名稱</param>
        /// <param name="dtStartMerge">開始整併的時間點(理論上應該丟入開始轉置的StartTime，防止有第一次整併的新資料進來卻被Update掉)</param>
        public static void updIsMergeTrue(string strTableName, string strStartMergeTime)
        {
            try
            {
                SqlConnection Conn = new SqlConnection(strPrimaryDBConn);
                SqlCommand Cmd = Conn.CreateCommand();
                Conn.Open();
                Cmd.CommandTimeout = 3000;
                Cmd.CommandText = "UPDATE " + strTableName + " SET IsMerge=1 WHERE UpdateDate<=@UpdateDate";
                Cmd.Parameters.Clear();
                Cmd.Parameters.AddWithValue("@UpdateDate", strStartMergeTime);
                Cmd.ExecuteNonQuery();

                Conn.Dispose();
                if (Conn != null)
                    Conn.Close();
                Cmd.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        /// <summary>
        /// 將正規化資料表的合併狀態設置為已合併。
        /// </summary>
        /// <param name="strTableName">資料表名稱</param>
        /// <param name="dtStartMerge">開始整併的時間點(理論上應該丟入開始轉置的StartTime，防止有第一次整併的新資料進來卻被Update掉)</param>
        /// <param name="strMergeTempSN">搭配IsMerge給予第二次整併使用，用來做為分批取值的唯一值，該數值以當前時間為主，例如：DataTime.Now.ToString("yyMMddHHmmssfff")</param>
        public static void updIsMergeTrue(string strTableName, string strStartMergeTime, string strMergeTempSN)
        {
            try
            {
                SqlConnection Conn = new SqlConnection(strPrimaryDBConn);
                SqlCommand Cmd = Conn.CreateCommand();
                Conn.Open();
                Cmd.CommandTimeout = 3000;
                //Cmd.CommandText = "UPDATE " + strTableName + " SET IsMerge=1 WHERE MergeTempSN=@MergeTempSN AND EXISTS" +
                //                  " ( SELECT UpdateDate FROM " + strTableName + " where UpdateDate<=@UpdateDate )";

                string strWhereCmd = "";

                Cmd.Parameters.Clear();

                if (strTableName != "EPASupplierCustomerInfo_Tox")
                {
                    strWhereCmd = " AND UpdateDate<=@UpdateDate";
                    Cmd.Parameters.AddWithValue("@UpdateDate", strStartMergeTime);
                }

                Cmd.CommandText = "UPDATE " + strTableName +
                                  " SET IsMerge='1' WHERE MergeTempSN=@MergeTempSN" + strWhereCmd;
                
                Cmd.Parameters.AddWithValue("@MergeTempSN", strMergeTempSN);
                
                Console.WriteLine("UPDATE IsMerge=1");
                Cmd.ExecuteNonQuery();

                Conn.Dispose();
                if (Conn != null)
                    Conn.Close();
                Cmd.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());

                //塞入單筆轉置錯誤的訊息。
                funcUtil.ins2ndDataTransErrorLog("-", "-", "updIsMergeTrue", ex.ToString(),
                    "將正規化資料表的合併狀態設置為已合併錯誤。來源表格：" + strTableName + ",MergeTempSN：" + strMergeTempSN);
            }
        }
        
        public static string Unicode2HTML(string input)
        {
            try
            {
                //先檢查是否有"&#"
                if (!input.Contains("&#"))
                {
                    return input;
                }

                StringBuilder sb = new StringBuilder();
                //以;將文字拆成陣列
                string[] tmp = input.Split(';');
                for (int i = 0; i < tmp.Length; i++)
                {
                    //以&#將文字拆成陣列
                    string[] tmp2 = tmp[i].Split(new string[] { "&#" }, StringSplitOptions.RemoveEmptyEntries);
                    if (tmp2.Length == 1)
                    {
                        //如果長度為1則試圖轉換UNICODE回字符，若失敗則使用原本的字元
                        if (i != tmp.Length - 1)
                        {
                            try
                            {
                                sb.Append(Convert.ToChar(Convert.ToInt32(int.Parse(tmp2[0]))).ToString());
                            }
                            catch
                            {
                                sb.Append(tmp2[0]);
                            }
                        }
                        else
                        {
                            sb.Append(tmp2[0]);
                        }
                    }
                    if (tmp2.Length == 2)
                    {
                        //若長度為2，則第一項不處理，只處理第二項即可
                        sb.Append(tmp2[0]);
                        sb.Append(Convert.ToChar(Convert.ToInt32(int.Parse(tmp2[1]))).ToString());
                    }
                }
                return sb.ToString();
            }
            catch (Exception)
            {
                return input;
            }
        }

        ///<summary>
        ///字串轉半形
        ///</summary>
        ///<paramname="input">任一字串</param>
        ///<returns>半形字元串</returns>
        public static string ToNarrow(string input)
        {
            char[] c = input.ToCharArray();
            for (int i = 0; i < c.Length; i++)
            {
                if (c[i] == 12288)
                {
                    c[i] = (char)32;
                    continue;
                }
                if (c[i] > 65280 && c[i] < 65375)
                    c[i] = (char)(c[i] - 65248);
            }
            
            return new string(c);
        }

        ///<summary>
        ///特殊字元取代
        ///</summary>
        public static string SpecialChange(string input)
        {
            if (input.Contains("’"))
            {
                input = input.Replace("’", "'").Replace("〝", "\"").Replace("〞", "\"");
            }

            return input;
        }

        public static string UnitConvert(string strQuantity, string strUnit)
        {
            var strResult = string.Empty;
            strUnit = strUnit.Trim();
            strQuantity = strQuantity.Trim();
            if (string.IsNullOrEmpty(strQuantity))
            {
                //若來源的數量沒有值，則回傳空白
                return strResult;
            }
            switch (strUnit)
            {
                case "公噸":
                    strResult = (Convert.ToDecimal(strQuantity) * 1000).ToString(CultureInfo.CurrentCulture);
                    
                    break;
                case "噸":
                    Decimal a = Convert.ToDecimal(strQuantity);
                    strResult = (Convert.ToDecimal(strQuantity) * 1000).ToString(CultureInfo.CurrentCulture);

                    break;
                case "公克":
                    strResult = (Math.Round(Convert.ToDecimal(strQuantity) / 1000, 12)).ToString(CultureInfo.CurrentCulture);
                    
                    break;
                case "磅":
                    strResult = (Math.Round(Convert.ToDecimal(strQuantity) / (decimal) 0.45359237, 12)).ToString(CultureInfo.CurrentCulture);
                   
                    break;
                case "公斤":
                    strResult = strQuantity;
                    break;
                case "kg":
                    strResult = strQuantity;
                    break;
                case "KG":
                    strResult = strQuantity;
                    break;
                case "KGM":
                    strResult = strQuantity;
                    break;
                default:
                    strResult = string.Empty;
                    break;
            }
            return strResult;
        }
        #endregion

        #region 倉儲轉置函數
        /// <summary>
        /// 插入資料轉置異常記錄-單筆(倉儲轉置)
        /// </summary>
        /// <param name="strNo">訊息代碼，TEMP DB的表格中的No欄位，記第幾筆。</param>
        /// <param name="strTransId">交易代碼(DataID+yyMMddHHmmssfff)</param>
        /// <param name="strTempTableName">暫存表格名稱</param>
        /// <param name="strErrorMessage">錯誤訊息</param>
        /// <param name="strComment">備註說明</param>
        public static void insDataTransErrorLog(string strNo, string strTransId,
                                                string strTempTableName, string strErrorMessage, string strComment)
        {
            try
            {
                SqlConnection Conn = new SqlConnection(strBaseDBConn);
                SqlCommand Cmd = Conn.CreateCommand();
                Conn.Open();
                Cmd.CommandTimeout = 3000;
                Cmd.CommandText = "INSERT INTO DataTransErrorLog (No, Phase, TransId, TempTableName, ErrorMessage, UpdateTime, Comment)";
                Cmd.CommandText += " VALUES (@No, @Phase, @TransId, @TempTableName, @ErrorMessage, @UpdateTime, @Comment)";
                Cmd.Parameters.Clear();
                Cmd.Parameters.AddWithValue("@No", strNo);
                Cmd.Parameters.AddWithValue("@Phase", "5");
                Cmd.Parameters.AddWithValue("@TransId", strTransId);
                Cmd.Parameters.AddWithValue("@TempTableName", strTempTableName);
                Cmd.Parameters.AddWithValue("@ErrorMessage", strErrorMessage);
                Cmd.Parameters.AddWithValue("@UpdateTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Cmd.Parameters.AddWithValue("@Comment", strComment);
                Cmd.ExecuteNonQuery();

                Conn.Dispose();
                if (Conn != null)
                    Conn.Close();
                Cmd.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        
        /// <summary>
        /// 插入資料轉置記錄-整批(倉儲轉置)
        /// </summary>
        /// <param name="strTransId">交易編號(DataId+yyMMddHHmmssfff)</param>
        /// <param name="strDataId">轉入資料集編號</param>
        /// <param name="strStartTime">轉入起始時間</param>
        /// <param name="strEndTime">轉入結束時間</param>
        /// <param name="strImportFileOrTable">轉入檔案或Table名稱(存到Primary DB的Table名稱)</param>
        /// <param name="intTotalCount">資料總筆數</param>
        /// <param name="intErrorCount">錯誤筆數</param>
        public static void insDataTransLog(string strStartTime, string strEndTime, string strImportFileOrTable,
                                           int intTotalCount, int intErrorCount)
        {
            //若轉置筆數超過 0 筆再插入Log。
            if (intTotalCount > 0)
            {
                try
                {
                    SqlConnection Conn = new SqlConnection(strBaseDBConn);
                    SqlCommand Cmd = Conn.CreateCommand();
                    Conn.Open();
                    Cmd.CommandTimeout = 3000;
                    Cmd.CommandText = "INSERT INTO DataTransLog (TransId, Phase, DataId, StartTime, EndTime, ImportFileOrTable, TotalCount, ErrorCount)";
                    Cmd.CommandText += " VALUES (@TransId, @Phase, @DataId, @StartTime, @EndTime, @ImportFileOrTable, @TotalCount, @ErrorCount)";
                    Cmd.Parameters.Clear();
                    Cmd.Parameters.AddWithValue("@TransId", "");
                    Cmd.Parameters.AddWithValue("@Phase", "5");
                    Cmd.Parameters.AddWithValue("@DataId", "");
                    Cmd.Parameters.AddWithValue("@StartTime", strStartTime);
                    Cmd.Parameters.AddWithValue("@EndTime", strEndTime);
                    Cmd.Parameters.AddWithValue("@ImportFileOrTable", strImportFileOrTable);
                    Cmd.Parameters.AddWithValue("@TotalCount", intTotalCount);
                    Cmd.Parameters.AddWithValue("@ErrorCount", intErrorCount);
                    Cmd.ExecuteNonQuery();

                    Conn.Dispose();
                    if (Conn != null)
                        Conn.Close();
                    Cmd.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }
        #endregion

        #region 資料驗證
        public static string CasNo(string s) 
        {
            Regex rgx = new Regex(@"[1-9]{1}[0-9]{1,5}-\d{2}-\d");
            if (!rgx.IsMatch(s))
                s = "-";
            return s;
        }

        //統編
        public static string AdminNo(string s)
        {
            Regex rgx = new Regex(@"[0-9]{8}");
            if (!rgx.IsMatch(s))
                s = "-";
            else if (s == "00000000")
                s = "-";
            return s;
        }

        //運作量科學記號或非數字轉換
        public static string Exponential(string s)
        {
            if (s != "")
            {
                try 
                {
                    decimal d = decimal.Parse(s, NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint);
                    s = d.ToString();
                    //尾數無效0
                    while (s.Contains(".") && (s.EndsWith("0") || s.EndsWith(".")))
                        s = s.Substring(0, s.Length - 1);
                }
                catch (Exception ex)
                {
                    //非數字字串
                    s = "";
                }
                return s;
            }
            else
                return s;
        }

        //運作量相加
        public static string QPlus(string a, string b) 
        {
            string q = "";
            decimal Qd=0;
            try
            {
                if (a == "" && b == "")
                    return q;
                else 
                {
                    if (a == "")
                        Qd = decimal.Parse(b);
                    else if (b == "")
                        Qd = decimal.Parse(a);
                    else
                        Qd = decimal.Parse(a) + decimal.Parse(b);
                }
                q = Exponential(Qd.ToString());
            }
            catch (Exception ex)
            {
            }
            return q;
        }
        #endregion


        public static string getChemiTradeDate(string methodName)
        {
            string strDate = "" ;
            try
            {
                SqlConnection Conn = new SqlConnection(strBaseDBConn);
                SqlCommand Cmd = Conn.CreateCommand();
                Conn.Open();
                Cmd.CommandTimeout = 3000;
                Cmd.CommandText = "SELECT ConfigValue FROM config WHERE ConfigName = 'ChemiTradeDate'";
                Cmd.Parameters.Clear();

                SqlDataReader dr = Cmd.ExecuteReader();

                if (dr.Read())
                {
                    string[] tpa = dr["ConfigValue"].ToString().Split(';');
                    foreach (string str in tpa)
                    {
                        if (str.IndexOf(methodName) > -1)
                        {
                            string[] tpa2 = str.Split(':');
                            strDate = tpa2[1];
                        }
                    }
                }
                if (dr != null)
                    dr.Close();

                Conn.Dispose();
                if (Conn != null)
                    Conn.Close();
                Cmd.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return strDate;
        }

    }
}