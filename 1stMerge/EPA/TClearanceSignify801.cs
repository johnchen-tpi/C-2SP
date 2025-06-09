using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Normalization_MS
{
    class TClearanceSignify801
    {

        public static string strTempDBConn = ConfigurationManager.AppSettings["TempDBConnectionStr"];   //ChemiTemp DB 連限字串
        public static string strPrimaryDBConn = ConfigurationManager.AppSettings["PrimaryDBConnectionStr"];   //ChemiTemp DB 連限字串

        public static string strStartTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"); //取得轉置開始時間
        public static string strTransId = "";     //交易編號
        public static int intDataTotalCount = 0;  //資料總筆數
        public static int intDataPage = 0;        //資料分頁筆數，因為資料量非常大，因此分批處理。
        public static int intErrorCount = 0;      //用來計算錯誤筆數
        public static string strCmd = "";
        public static SqlCommand CmdT;
        public static SqlCommand CmdP;
        public static SqlDataReader drP;
        public static int countNo = 1;

        /// <summary>
        /// 轉置 801通關簽審資料 (TClearanceSignify801) 資料集
        /// </summary>
        /// <param name="strTempTableName">Temp DB 轉置的表格名稱</param>
        public static void convertFunc(string strTempTableName)
        {

            //因為需要從 Temp DB 取出轉置的資料，需要定義 Temp DB 連線。
            SqlConnection ConnT = new SqlConnection(strTempDBConn);
            CmdT = ConnT.CreateCommand();
            ConnT.Open();
            CmdT.CommandTimeout = 3000;

            //因為需要轉置至 Primary DB 的資料，需要定義 Primary DB 連線。
            SqlConnection ConnP = new SqlConnection(strPrimaryDBConn);
            CmdP = ConnP.CreateCommand();
            ConnP.Open();
            CmdP.CommandTimeout = 0;// 3000;

            try
            {
                //Step 1：先取出交易編號及欲轉置的總資料量筆數。
                CmdT.CommandText = "SELECT TOP 1 COUNT(TransId) AS TotalCount, TransId FROM " + strTempTableName;
                //！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！
                CmdT.CommandText += " WHERE IsChecked=1 AND IsConverted=0";
                //！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！
                CmdT.CommandText += " GROUP BY TransId ORDER BY TransId";
                SqlDataReader drT = CmdT.ExecuteReader();
                if (drT.Read())
                {
                    intDataTotalCount = drT.GetInt32(0);
                    strTransId = drT["TransId"].ToString().Trim();
                }
                if (drT != null)
                    drT.Close();


                //Step 2：若取出的交易編號不為空白，表示尚有資料還需要轉置，則將開始進行以下轉置動作。
                if (strTransId != "")
                {
                    Console.WriteLine("取得轉置交易編號：" + strTransId + "，總資料量共" + intDataTotalCount.ToString() + "筆。");

                    //Step 3：依據總筆數分批抓出欲轉置的資料。
                    //CmdT.CommandText = "SELECT * FROM " + strTempTableName;
                    //CmdT.CommandText += " WHERE TransId=@TransId AND IsChecked=1 AND IsConverted=0";
                    //CmdT.CommandText += " ORDER BY No";
                    CmdT.CommandText = "SELECT distinct a.*,b.ConstituentCName,b.ConstituentEName,b.CasNo,b.TransId tid FROM"
                        + "(select * from TClearanceSignify801 WHERE TransId=@TransId AND IsChecked=1 AND IsConverted=0)a "
                        + "left join (select * from TProductING801 where IsChecked=1 AND IsConverted=0) b "
                        + "on a.ID=b.ID ORDER BY a.No";
                    SqlDataAdapter daData = new SqlDataAdapter(CmdT.CommandText, ConnT);
                    daData.SelectCommand.CommandTimeout = 300000;
                    daData.SelectCommand.Parameters.Clear();
                    daData.SelectCommand.Parameters.AddWithValue("@TransId", strTransId);

                    DataTable dtTempDataResult = new DataTable();
                    //※※※※※※ 分批載入從 Temp DB 取得到的資料至暫存的 DataTable。※※※※※※
                    daData.Fill(dtTempDataResult);

                    int intDataCount = dtTempDataResult.Rows.Count; //傳入資料總筆數

                    bool blDeleteDataIsSuccess = false;
                    if (intDataCount > 0)
                    {
                        try
                        {
                            //目前此表無其他用途暫不處理
                            //strCmd = @"TRUNCATE TABLE EPAClearanceSignify801";
                            //CmdP.CommandText = strCmd;
                            //CmdP.Parameters.Clear();
                            //CmdP.ExecuteNonQuery();

                            strCmd = @"DELETE FROM EPAChemiComMapping WHERE TempTableName = 'TClearanceSignify801'";
                            CmdP.CommandText = strCmd;
                            CmdP.Parameters.Clear();
                            CmdP.ExecuteNonQuery();

                            strCmd = @"DELETE FROM EPASupplierCustomerInfo WHERE TempTableName = 'TClearanceSignify801'";
                            CmdP.CommandText = strCmd;
                            CmdP.Parameters.Clear();
                            CmdP.ExecuteNonQuery();

                            strCmd = @"DELETE FROM EPAChemiCredential WHERE TempTableName = 'TClearanceSignify801'";
                            CmdP.CommandText = strCmd;
                            CmdP.Parameters.Clear();
                            CmdP.ExecuteNonQuery();

                            strCmd = @"DELETE FROM ChemiComMapping WHERE TempTableName = 'TClearanceSignify801'";
                            CmdP.CommandText = strCmd;
                            CmdP.Parameters.Clear();
                            CmdP.ExecuteNonQuery();

                            strCmd = @"DELETE FROM SupplierCustomerInfo WHERE TableName = 'TClearanceSignify801'";
                            CmdP.CommandText = strCmd;
                            CmdP.Parameters.Clear();
                            CmdP.ExecuteNonQuery();

                            strCmd = @"DELETE FROM ChemiCredential WHERE TempTableName = 'TClearanceSignify801'";
                            CmdP.CommandText = strCmd;
                            CmdP.Parameters.Clear();
                            CmdP.ExecuteNonQuery();

                            blDeleteDataIsSuccess = true;
                        }
                        catch (Exception ex)
                        {
                            //插入轉置失敗Log。
                            intErrorCount++;    //錯誤筆數+1
                            funcUtil.ins1stDataTransErrorLog("-", strTransId,
                                                 strTempTableName, ex.Message, "清空資料相關表格失敗。");
                        }
                    }

                    //清空資料成功才轉置
                    if (blDeleteDataIsSuccess)
                    {
                        string trID = "";
                        //比對所有資料(批次性)，每次只載入 intDataCount 筆資料。
                        for (int intCount = 0; intCount < intDataCount; intCount++)
                        {
                            Console.Write("\r\b\b\b\b\b\b" + (countNo++) + "/" + intDataCount.ToString() + "\t\t \b\b" + intErrorCount + "/錯誤筆數");

                            try
                            {
                                ////
                                //由 dtTempDataResult.Rows[intCount]["欄位名稱"].ToString()取得該筆資料進行後續比對處理。
                                ////
                                #region 化學物質資料比對。
                                bool blChemicalDataIsExist = false;    //判斷該筆資料的化學物質資料是否存在。
                                                                                            
                                //string strChemicalChnName = dtTempDataResult.Rows[intCount]["MajorConstituentCName"].ToString();
                                //string strChemicalEngName = dtTempDataResult.Rows[intCount]["MajorConstituentEName"].ToString();
                                //string strCASNo = "-";                                                                
                                //switch (strCASNoTemp)
                                //{
                                //    case "其他:無":
                                //    case "其他:尚未分類":
                                //    case "其他:已登錄於化學物質之法規":
                                //    case "其他:2-(2-BUTOXYETHOXY)ETHANOL":
                                //    case "其他:無化學物質":
                                //    case "":
                                //        strCASNo = "-";
                                //        break;

                                //    default:
                                //        strCASNo = strCASNoTemp.Replace("其他:CAS.NO.", "").Replace("其他:","");
                                //        break;
                                //}
                                string strNo = dtTempDataResult.Rows[intCount]["No"].ToString();
                                string strChemicalChnName = "";
                                string strChemicalEngName = "";
                                string strCASNo = "";
                                string strCASNoTemp = dtTempDataResult.Rows[intCount]["Cas_No"].ToString();
                                strCASNoTemp = strCASNoTemp.Replace("其他:", "");

                                //TProductING801比對不到化學物質時以原表資料
                                if (dtTempDataResult.Rows[intCount]["ConstituentCName"].ToString() == "")
                                {
                                    strChemicalChnName = dtTempDataResult.Rows[intCount]["MajorConstituentCName"].ToString();
                                    strChemicalEngName = dtTempDataResult.Rows[intCount]["MajorConstituentEName"].ToString();
                                    strCASNo = getCAS(strCASNoTemp);
                                }
                                else
                                {
                                    trID = dtTempDataResult.Rows[intCount]["tid"].ToString();
                                    strChemicalChnName = dtTempDataResult.Rows[intCount]["ConstituentCName"].ToString();
                                    strChemicalEngName = dtTempDataResult.Rows[intCount]["ConstituentEName"].ToString();
                                    strCASNo = dtTempDataResult.Rows[intCount]["CasNo"].ToString();
                                }
                                if (strCASNo == "")
                                    strCASNo = "-";
                                
                                strCmd = @"SELECT * FROM EPAChemicalData WHERE ChemicalChnName=@ChemicalChnName AND 
                                            ChemicalEngName=@ChemicalEngName AND CASNo=@CASNo AND TempTableName=@TempTableName";

                                CmdP.CommandText = strCmd;
                                CmdP.Parameters.Clear();
                                CmdP.Parameters.AddWithValue("@ChemicalChnName", strChemicalChnName);
                                CmdP.Parameters.AddWithValue("@ChemicalEngName", strChemicalEngName);
                                CmdP.Parameters.AddWithValue("@CASNo", strCASNo);
                                CmdP.Parameters.AddWithValue("@TempTableName", strTempTableName);

                                drP = CmdP.ExecuteReader();
                                if (drP.Read())
                                {
                                    blChemicalDataIsExist = true;
                                }
                                if (drP != null)
                                    drP.Close();
                                #endregion

                                #region 若不存在則需要將該筆資料新增(Insert)至化學物質基本資料的表格中。
                                if (!blChemicalDataIsExist)
                                {
                                    strCmd = @"INSERT INTO EPAChemicalData (ChemicalEngName, ChemicalChnName, CASNo, TransId, TempTableName, No) 
                                               VALUES (@ChemicalEngName, @ChemicalChnName, @CASNo, @TransId, @TempTableName, @No)";
                                    CmdP.CommandText = strCmd;
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@ChemicalChnName", strChemicalChnName);
                                    CmdP.Parameters.AddWithValue("@ChemicalEngName", strChemicalEngName);
                                    CmdP.Parameters.AddWithValue("@CASNo", strCASNo);
                                    CmdP.Parameters.AddWithValue("@TransId", strTransId);
                                    CmdP.Parameters.AddWithValue("@TempTableName", strTempTableName);
                                    CmdP.Parameters.AddWithValue("@No", strNo);

                                    try
                                    {
                                        CmdP.ExecuteNonQuery();
                                        //strChemicalEngName = dtTempDataResult.Rows[intCount][strChemicalChnName].ToString();
                                        //Console.WriteLine("新增 化學物質：" + dtTempDataResult.Rows[intCount]["No"] + " 資料至化學物質的表格中。");
                                    }
                                    catch (Exception ex)
                                    {
                                        //插入轉置失敗Log。
                                        intErrorCount++;    //錯誤筆數+1
                                        funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strTempTableName, ex.Message, 
                                            "新增資料至化學物質的表格失敗。");
                                        continue;
                                    }
                                }
                                #endregion

                                #region 若存在則需要將該筆資料更新(Update)至化學物質基本資料的表格中。
                                if (blChemicalDataIsExist)
                                {
                                    strCmd = @"UPDATE EPAChemicalData SET UpdateDate=@UpdateDate, TransId=@TransId, No=@No, IsMerge='0'
                                                WHERE ChemicalEngName=@ChemicalEngName AND ChemicalChnName=@ChemicalChnName AND CASNo=@CASNo AND TempTableName=@TempTableName";
                                    CmdP.CommandText = strCmd;
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@UpdateDate", strStartTime);
                                    CmdP.Parameters.AddWithValue("@TransId", strTransId);
                                    CmdP.Parameters.AddWithValue("@No", strNo);
                                    CmdP.Parameters.AddWithValue("@ChemicalChnName", strChemicalChnName);
                                    CmdP.Parameters.AddWithValue("@ChemicalEngName", strChemicalEngName);
                                    CmdP.Parameters.AddWithValue("@CASNo", strCASNo);
                                    CmdP.Parameters.AddWithValue("@TempTableName", strTempTableName);

                                    try
                                    {
                                        CmdP.ExecuteNonQuery();
                                        //Console.WriteLine("更新 化學物質：" + strChemicalChnName + " 資料至化學物質的表格中。");
                                    }
                                    catch (Exception ex)
                                    {
                                        //插入轉置失敗Log。
                                        intErrorCount++;    //錯誤筆數+1
                                        funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strTempTableName, ex.Message, 
                                            "更新資料至化學物質的表格失敗。");
                                        continue;
                                    }
                                }
                                #endregion

                                #region Mapping表比對。
                                string strEmsNo = "-";

                                string strFactoryRegNo = "-";
                                string strBusinessAdminNo = dtTempDataResult.Rows[intCount]["Comp_Id"].ToString();
                                string strComFacBizName = dtTempDataResult.Rows[intCount]["CName"].ToString();
                                string strComFacBizAddr = dtTempDataResult.Rows[intCount]["CAdd"].ToString();

                                string comAdminTemp = ComFacBizMatch.MainFunc(strEmsNo, strBusinessAdminNo, strFactoryRegNo, strComFacBizName, strComFacBizAddr, strTransId, strTempTableName, dtTempDataResult.Rows[intCount]["No"].ToString());
                                string strComFacBizType = comAdminTemp.Split(',')[0];
                                string strAdminNo = comAdminTemp.Split(',')[1];

                                if (strComFacBizType == "" || strAdminNo == "")
                                {
                                    funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strTempTableName, 
                                        "801通關簽審資料(TClearanceSignify801), EPAChemiComMapping表轉換AdminNo出現問題，AdminNo或ComFacBizType轉換後為空白", "新增資料至化學物質公司工廠對應的表格失敗。");
                                    intErrorCount++;
                                    continue;
                                }

                                bool blMappingIsExist = false;
                                string strMappingSN = "";

                                strCmd = @"SELECT MappingSN FROM EPAChemiComMapping WHERE 
                                    TempTableName=@TempTableName AND ChemicalEngName=@ChemicalEngName AND ChemicalChnName=@ChemicalChnName AND 
                                    CASNo=@CASNo AND ComFacBizType=@ComFacBizType AND AdminNo=@AdminNo";

                                CmdP.CommandText = strCmd;
                                CmdP.Parameters.Clear();
                                CmdP.Parameters.AddWithValue("@TempTableName", strTempTableName);
                                CmdP.Parameters.AddWithValue("@ChemicalEngName", strChemicalEngName);
                                CmdP.Parameters.AddWithValue("@ChemicalChnName", strChemicalChnName);
                                CmdP.Parameters.AddWithValue("@CASNo", strCASNo);
                                CmdP.Parameters.AddWithValue("@ComFacBizType", strComFacBizType);
                                CmdP.Parameters.AddWithValue("@AdminNo", strAdminNo);

                                drP = CmdP.ExecuteReader();
                                if (drP.Read())
                                {
                                    blMappingIsExist = true;
                                    strMappingSN = drP["MappingSN"].ToString().Trim();
                                }
                                if (drP != null)
                                    drP.Close();
                                #endregion

                                #region 若不存在則需要將該筆資料新增(Insert)至化學物質公司工廠對應資料的表格中。
                                if (!blMappingIsExist)
                                {
                                    strMappingSN = strTransId + strNo;

                                    strCmd = @"INSERT INTO EPAChemiComMapping (MappingSN, TempTableName, ChemicalEngName, ChemicalChnName, CASNo, ComFacBizType, AdminNo) 
                                                VALUES (@MappingSN, @TempTableName, @ChemicalEngName, @ChemicalChnName, @CASNo, @ComFacBizType, @AdminNo)";

                                    CmdP.CommandText = strCmd;
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@MappingSN", strMappingSN);
                                    CmdP.Parameters.AddWithValue("@TempTableName", strTempTableName);
                                    CmdP.Parameters.AddWithValue("@ChemicalEngName", strChemicalEngName);
                                    CmdP.Parameters.AddWithValue("@ChemicalChnName", strChemicalChnName);
                                    CmdP.Parameters.AddWithValue("@CASNo", strCASNo);
                                    CmdP.Parameters.AddWithValue("@ComFacBizType", strComFacBizType);
                                    CmdP.Parameters.AddWithValue("@AdminNo", strAdminNo);

                                    try
                                    {
                                        CmdP.ExecuteNonQuery();
                                        //Console.WriteLine("新增 化學物質公司工廠對應資料：" + strMappingSN + " 資料至表格中。");
                                    }
                                    catch (Exception ex)
                                    {
                                        //插入轉置失敗Log。
                                        intErrorCount++;    //錯誤筆數+1
                                        funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strTempTableName, ex.Message, 
                                            "新增資料至化學物質公司工廠對應的表格失敗。");
                                        continue;
                                    }
                                }
                                #endregion

                                #region 運作量資料比對。

                                #region (strSupplierCustomerType = 0)運作量資料比對。
                                #region 運作量參數設定
                                string strSupplierCustomerType = "0";

                                string strSCComFacBizType = "-";
                                string strSCAdminNo = "-";

                                string strDeclareTime = dtTempDataResult.Rows[intCount]["Apply_Date"].ToString();

                                DateTime dt = Convert.ToDateTime(strDeclareTime);

                                string year = (dt.Year - 1911).ToString();
                                string month = dt.Month.ToString();
                                string day = dt.Day.ToString();

                                

                                string strDeclareYearS = year;
                                string strDeclareMonthS = month;
                                string strDeclareDayS = day;
                                string strDeclareSeasonS = transMonthToSeason(month);
                                string strDeclareYearE = year;
                                string strDeclareMonthE = month;
                                string strDeclareDayE = day;
                                string strDeclareSeasonE = transMonthToSeason(month);

                                string strQty = dtTempDataResult.Rows[intCount]["Qty"].ToString();
                                string strQuantityUnit = dtTempDataResult.Rows[intCount]["Qty_Unit"].ToString();

                                string strID = dtTempDataResult.Rows[intCount]["ID"].ToString();
                                string strTradeVan_Id = dtTempDataResult.Rows[intCount]["TradeVan_Id"].ToString();

                                string strChemicalPK = strID + "@" + strTradeVan_Id ;

                                //////////////////取得來源資料PK對應表流水號/////////////////////
                                string strOriginalPK = strChemicalPK;
                                string strOriginalSN = funcUtil.OriginalMapping_Convert(strOriginalPK, strTempTableName, strTransId, dtTempDataResult.Rows[intCount]["No"].ToString());
                                ////////////////////////////////////////////////////////////////


                                bool blTWaterPollutionExam = false;    //判斷該筆資料的上下游資料是否存在。
                                #endregion

                                #region 801通關簽審資料 (TClearanceSignify801)判斷是否存在
                                strCmd = @"SELECT * FROM EPASupplierCustomerInfo WHERE 
                                            MappingSN=@MappingSN AND TempTableName=@TempTableName AND SupplierCustomerType=@SupplierCustomerType AND
                                            SCComFacBizType=@SCComFacBizType AND SCAdminNo=@SCAdminNo AND DeclareYearS=@DeclareYearS AND DeclareMonthS=@DeclareMonthS AND DeclareDayS=@DeclareDayS AND 
                                            DeclareSeasonS=@DeclareSeasonS AND DeclareYearE=@DeclareYearE AND DeclareMonthE=@DeclareMonthE AND DeclareDayE=@DeclareDayE AND 
                                            DeclareSeasonE=@DeclareSeasonE AND OriginalSN=@OriginalSN";

                                CmdP.CommandText = strCmd;
                                CmdP.Parameters.Clear();

                                CmdP.Parameters.AddWithValue("@MappingSN", strMappingSN);
                                CmdP.Parameters.AddWithValue("@TempTableName", strTempTableName);
                                CmdP.Parameters.AddWithValue("@SupplierCustomerType", strSupplierCustomerType);
                                CmdP.Parameters.AddWithValue("@SCComFacBizType", strSCComFacBizType);
                                CmdP.Parameters.AddWithValue("@SCAdminNo", strSCAdminNo);
                                CmdP.Parameters.AddWithValue("@DeclareYearS", strDeclareYearS);
                                CmdP.Parameters.AddWithValue("@DeclareMonthS", strDeclareMonthS);
                                CmdP.Parameters.AddWithValue("@DeclareDayS", strDeclareDayS);
                                CmdP.Parameters.AddWithValue("@DeclareSeasonS", strDeclareSeasonS);
                                CmdP.Parameters.AddWithValue("@DeclareYearE", strDeclareYearE);
                                CmdP.Parameters.AddWithValue("@DeclareMonthE", strDeclareMonthE);
                                CmdP.Parameters.AddWithValue("@DeclareDayE", strDeclareDayE);
                                CmdP.Parameters.AddWithValue("@DeclareSeasonE", strDeclareSeasonE);
                                CmdP.Parameters.AddWithValue("@OriginalSN", strOriginalSN);

                                drP = CmdP.ExecuteReader();
                                if (drP.Read())
                                {
                                    blTWaterPollutionExam = true;
                                }
                                if (drP != null)
                                    drP.Close();
                                #endregion

                                #region 若該筆上下游資料不存在，則Insert。
                                if (!blTWaterPollutionExam)
                                {
                                    strCmd = @"INSERT INTO EPASupplierCustomerInfo (MappingSN, TempTableName, SupplierCustomerType, SCComFacBizType, SCAdminNo, DeclareYearS, DeclareMonthS, DeclareDayS,
                                            DeclareSeasonS, DeclareYearE, DeclareMonthE, DeclareDayE, DeclareSeasonE, OriginalSN, Qty, QuantityUnit, UpdateDate, TransId, No) 
                                            VALUES (@MappingSN, @TempTableName, @SupplierCustomerType, @SCComFacBizType, @SCAdminNo, @DeclareYearS, @DeclareMonthS, @DeclareDayS, @DeclareSeasonS,
                                            @DeclareYearE, @DeclareMonthE, @DeclareDayE, @DeclareSeasonE, @OriginalSN, @Qty, @QuantityUnit, @UpdateDate, @TransId, @No)";

                                    CmdP.CommandText = strCmd;
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@MappingSN", strMappingSN);
                                    CmdP.Parameters.AddWithValue("@TempTableName", strTempTableName);
                                    CmdP.Parameters.AddWithValue("@SupplierCustomerType", strSupplierCustomerType);
                                    CmdP.Parameters.AddWithValue("@SCComFacBizType", strSCComFacBizType);
                                    CmdP.Parameters.AddWithValue("@SCAdminNo", strSCAdminNo);
                                    CmdP.Parameters.AddWithValue("@DeclareYearS", strDeclareYearS);
                                    CmdP.Parameters.AddWithValue("@DeclareMonthS", strDeclareMonthS);
                                    CmdP.Parameters.AddWithValue("@DeclareDayS", strDeclareDayS);
                                    CmdP.Parameters.AddWithValue("@DeclareSeasonS", strDeclareSeasonS);
                                    CmdP.Parameters.AddWithValue("@DeclareYearE", strDeclareYearE);
                                    CmdP.Parameters.AddWithValue("@DeclareMonthE", strDeclareMonthE);
                                    CmdP.Parameters.AddWithValue("@DeclareDayE", strDeclareDayE);
                                    CmdP.Parameters.AddWithValue("@DeclareSeasonE", strDeclareSeasonE);
                                    CmdP.Parameters.AddWithValue("@OriginalSN", strOriginalSN);

                                    CmdP.Parameters.AddWithValue("@Qty", strQty);
                                    CmdP.Parameters.AddWithValue("@QuantityUnit", strQuantityUnit);

                                    CmdP.Parameters.AddWithValue("@UpdateDate", strStartTime);
                                    CmdP.Parameters.AddWithValue("@TransId", strTransId);
                                    CmdP.Parameters.AddWithValue("@No", strNo);

                                    try
                                    {
                                        CmdP.ExecuteNonQuery();
                                    }
                                    catch (Exception ex)
                                    {
                                        //插入轉置失敗Log。
                                        intErrorCount++;    //錯誤筆數+1
                                        funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strTempTableName, ex.Message, 
                                            "新增 801通關簽審資料 (TClearanceSignify801)：失敗。");
                                        continue;
                                    }
                                }
                                #endregion

                                #region 若該筆上下游資料存在，則Update。
                                if (blTWaterPollutionExam)
                                {
                                    strCmd = @"UPDATE EPASupplierCustomerInfo SET Qty=@Qty, QuantityUnit=@QuantityUnit,
                                            UpdateDate=@UpdateDate, TransId=@TransId, IsMerge='0', No=@No 
                                            WHERE MappingSN=@MappingSN AND TempTableName=@TempTableName AND SupplierCustomerType=@SupplierCustomerType AND
                                            SCComFacBizType=@SCComFacBizType AND SCAdminNo=@SCAdminNo AND DeclareYearS=@DeclareYearS AND DeclareMonthS=@DeclareMonthS AND DeclareDayS=@DeclareDayS AND 
                                            DeclareSeasonS=@DeclareSeasonS AND DeclareYearE=@DeclareYearE AND DeclareMonthE=@DeclareMonthE AND DeclareDayE=@DeclareDayE AND 
                                            DeclareSeasonE=@DeclareSeasonE AND OriginalSN=@OriginalSN ";

                                    CmdP.CommandText = strCmd;
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@Qty", strQty);
                                    CmdP.Parameters.AddWithValue("@QuantityUnit", strQuantityUnit);

                                    CmdP.Parameters.AddWithValue("@UpdateDate", strStartTime);
                                    CmdP.Parameters.AddWithValue("@TransId", strTransId);
                                    CmdP.Parameters.AddWithValue("@No", strNo);

                                    CmdP.Parameters.AddWithValue("@MappingSN", strMappingSN);
                                    CmdP.Parameters.AddWithValue("@TempTableName", strTempTableName);
                                    CmdP.Parameters.AddWithValue("@SupplierCustomerType", strSupplierCustomerType);
                                    CmdP.Parameters.AddWithValue("@SCComFacBizType", strSCComFacBizType);
                                    CmdP.Parameters.AddWithValue("@SCAdminNo", strSCAdminNo);
                                    CmdP.Parameters.AddWithValue("@DeclareYearS", strDeclareYearS);
                                    CmdP.Parameters.AddWithValue("@DeclareMonthS", strDeclareMonthS);
                                    CmdP.Parameters.AddWithValue("@DeclareDayS", strDeclareDayS);
                                    CmdP.Parameters.AddWithValue("@DeclareSeasonS", strDeclareSeasonS);
                                    CmdP.Parameters.AddWithValue("@DeclareYearE", strDeclareYearE);
                                    CmdP.Parameters.AddWithValue("@DeclareMonthE", strDeclareMonthE);
                                    CmdP.Parameters.AddWithValue("@DeclareDayE", strDeclareDayE);
                                    CmdP.Parameters.AddWithValue("@DeclareSeasonE", strDeclareSeasonE);
                                    CmdP.Parameters.AddWithValue("@OriginalSN", strOriginalSN);

                                    try
                                    {
                                        CmdP.ExecuteNonQuery();
                                    }
                                    catch (Exception ex)
                                    {
                                        //插入轉置失敗Log。
                                        intErrorCount++;    //錯誤筆數+1
                                        funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strTempTableName, ex.Message, 
                                            "更新 801通關簽審資料 (TClearanceSignify801)：失敗。");
                                        continue;
                                    }
                                }
                                #endregion
                                #endregion

                                #region (strSupplierCustomerType = 1)運作量資料比對。
                                #region 運作量參數設定
                                strSupplierCustomerType = "1";

                                string strIName = dtTempDataResult.Rows[intCount]["IName"].ToString();
                                string strIAdd = dtTempDataResult.Rows[intCount]["IAdd"].ToString();


                                if (!String.IsNullOrWhiteSpace(strIName))
                                {                                    
                                    string sComAdminTemp = ComFacBizMatch.MainFunc("-", "-", "-", strIName, strIAdd, strTransId, strTempTableName, dtTempDataResult.Rows[intCount]["No"].ToString());
                                    strSCComFacBizType = sComAdminTemp.Split(',')[0];
                                    strSCAdminNo = sComAdminTemp.Split(',')[1];

                                    if (strSCComFacBizType == "" || strSCAdminNo == "")
                                    {
                                        funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strTempTableName, 
                                            "EPASupplierCustomerInfo轉換下游AdminNo出現問題，AdminNo或ComFacBizType轉換後為空白", "新增資料至運作資訊的表格失敗。");
                                        intErrorCount++;
                                        strSCComFacBizType = "-";
                                        strSCAdminNo = "-";
                                    }
                                }

                                blTWaterPollutionExam = false;    //判斷該筆資料的上下游資料是否存在。
                                #endregion

                                #region 801通關簽審資料 (TClearanceSignify801)判斷是否存在
                                strCmd = @"SELECT * FROM EPASupplierCustomerInfo WHERE 
                                            MappingSN=@MappingSN AND TempTableName=@TempTableName AND SupplierCustomerType=@SupplierCustomerType AND
                                            SCComFacBizType=@SCComFacBizType AND SCAdminNo=@SCAdminNo AND DeclareYearS=@DeclareYearS AND DeclareMonthS=@DeclareMonthS AND DeclareDayS=@DeclareDayS AND 
                                            DeclareSeasonS=@DeclareSeasonS AND DeclareYearE=@DeclareYearE AND DeclareMonthE=@DeclareMonthE AND DeclareDayE=@DeclareDayE AND 
                                            DeclareSeasonE=@DeclareSeasonE AND OriginalSN=@OriginalSN";

                                CmdP.CommandText = strCmd;
                                CmdP.Parameters.Clear();

                                CmdP.Parameters.AddWithValue("@MappingSN", strMappingSN);
                                CmdP.Parameters.AddWithValue("@TempTableName", strTempTableName);
                                CmdP.Parameters.AddWithValue("@SupplierCustomerType", strSupplierCustomerType);
                                CmdP.Parameters.AddWithValue("@SCComFacBizType", strSCComFacBizType);
                                CmdP.Parameters.AddWithValue("@SCAdminNo", strSCAdminNo);
                                CmdP.Parameters.AddWithValue("@DeclareYearS", strDeclareYearS);
                                CmdP.Parameters.AddWithValue("@DeclareMonthS", strDeclareMonthS);
                                CmdP.Parameters.AddWithValue("@DeclareDayS", strDeclareDayS);
                                CmdP.Parameters.AddWithValue("@DeclareSeasonS", strDeclareSeasonS);
                                CmdP.Parameters.AddWithValue("@DeclareYearE", strDeclareYearE);
                                CmdP.Parameters.AddWithValue("@DeclareMonthE", strDeclareMonthE);
                                CmdP.Parameters.AddWithValue("@DeclareDayE", strDeclareDayE);
                                CmdP.Parameters.AddWithValue("@DeclareSeasonE", strDeclareSeasonE);
                                CmdP.Parameters.AddWithValue("@OriginalSN", strOriginalSN);

                                drP = CmdP.ExecuteReader();
                                if (drP.Read())
                                {
                                    blTWaterPollutionExam = true;
                                }
                                if (drP != null)
                                    drP.Close();
                                #endregion

                                #region 若該筆上下游資料不存在，則Insert。
                                if (!blTWaterPollutionExam)
                                {
                                    strCmd = @"INSERT INTO EPASupplierCustomerInfo (MappingSN, TempTableName, SupplierCustomerType, SCComFacBizType, SCAdminNo, DeclareYearS, DeclareMonthS, DeclareDayS,
                                            DeclareSeasonS, DeclareYearE, DeclareMonthE, DeclareDayE, DeclareSeasonE, OriginalSN, Qty, QuantityUnit, UpdateDate, TransId, No) 
                                            VALUES (@MappingSN, @TempTableName, @SupplierCustomerType, @SCComFacBizType, @SCAdminNo, @DeclareYearS, @DeclareMonthS, @DeclareDayS, @DeclareSeasonS,
                                            @DeclareYearE, @DeclareMonthE, @DeclareDayE, @DeclareSeasonE, @OriginalSN, @Qty, @QuantityUnit, @UpdateDate, @TransId, @No)";

                                    CmdP.CommandText = strCmd;
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@MappingSN", strMappingSN);
                                    CmdP.Parameters.AddWithValue("@TempTableName", strTempTableName);
                                    CmdP.Parameters.AddWithValue("@SupplierCustomerType", strSupplierCustomerType);
                                    CmdP.Parameters.AddWithValue("@SCComFacBizType", strSCComFacBizType);
                                    CmdP.Parameters.AddWithValue("@SCAdminNo", strSCAdminNo);
                                    CmdP.Parameters.AddWithValue("@DeclareYearS", strDeclareYearS);
                                    CmdP.Parameters.AddWithValue("@DeclareMonthS", strDeclareMonthS);
                                    CmdP.Parameters.AddWithValue("@DeclareDayS", strDeclareDayS);
                                    CmdP.Parameters.AddWithValue("@DeclareSeasonS", strDeclareSeasonS);
                                    CmdP.Parameters.AddWithValue("@DeclareYearE", strDeclareYearE);
                                    CmdP.Parameters.AddWithValue("@DeclareMonthE", strDeclareMonthE);
                                    CmdP.Parameters.AddWithValue("@DeclareDayE", strDeclareDayE);
                                    CmdP.Parameters.AddWithValue("@DeclareSeasonE", strDeclareSeasonE);
                                    CmdP.Parameters.AddWithValue("@OriginalSN", strOriginalSN);

                                    CmdP.Parameters.AddWithValue("@Qty", strQty);
                                    CmdP.Parameters.AddWithValue("@QuantityUnit", strQuantityUnit);

                                    CmdP.Parameters.AddWithValue("@UpdateDate", strStartTime);
                                    CmdP.Parameters.AddWithValue("@TransId", strTransId);
                                    CmdP.Parameters.AddWithValue("@No", strNo);

                                    try
                                    {
                                        CmdP.ExecuteNonQuery();
                                    }
                                    catch (Exception ex)
                                    {
                                        //插入轉置失敗Log。
                                        intErrorCount++;    //錯誤筆數+1
                                        funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strTempTableName, ex.Message, 
                                            "新增 801通關簽審資料 (TClearanceSignify801)：失敗。");
                                        continue;
                                    }
                                }
                                #endregion

                                #region 若該筆上下游資料存在，則Update。
                                if (blTWaterPollutionExam)
                                {
                                    strCmd = @"UPDATE EPASupplierCustomerInfo SET Qty=@Qty, QuantityUnit=@QuantityUnit,
                                            UpdateDate=@UpdateDate, TransId=@TransId, IsMerge='0', No=@No 
                                            WHERE MappingSN=@MappingSN AND TempTableName=@TempTableName AND SupplierCustomerType=@SupplierCustomerType AND
                                            SCComFacBizType=@SCComFacBizType AND SCAdminNo=@SCAdminNo AND DeclareYearS=@DeclareYearS AND DeclareMonthS=@DeclareMonthS AND DeclareDayS=@DeclareDayS AND 
                                            DeclareSeasonS=@DeclareSeasonS AND DeclareYearE=@DeclareYearE AND DeclareMonthE=@DeclareMonthE AND DeclareDayE=@DeclareDayE AND 
                                            DeclareSeasonE=@DeclareSeasonE AND OriginalSN=@OriginalSN ";

                                    CmdP.CommandText = strCmd;
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@Qty", strQty);
                                    CmdP.Parameters.AddWithValue("@QuantityUnit", strQuantityUnit);

                                    CmdP.Parameters.AddWithValue("@UpdateDate", strStartTime);
                                    CmdP.Parameters.AddWithValue("@TransId", strTransId);
                                    CmdP.Parameters.AddWithValue("@No", strNo);

                                    CmdP.Parameters.AddWithValue("@MappingSN", strMappingSN);
                                    CmdP.Parameters.AddWithValue("@TempTableName", strTempTableName);
                                    CmdP.Parameters.AddWithValue("@SupplierCustomerType", strSupplierCustomerType);
                                    CmdP.Parameters.AddWithValue("@SCComFacBizType", strSCComFacBizType);
                                    CmdP.Parameters.AddWithValue("@SCAdminNo", strSCAdminNo);
                                    CmdP.Parameters.AddWithValue("@DeclareYearS", strDeclareYearS);
                                    CmdP.Parameters.AddWithValue("@DeclareMonthS", strDeclareMonthS);
                                    CmdP.Parameters.AddWithValue("@DeclareDayS", strDeclareDayS);
                                    CmdP.Parameters.AddWithValue("@DeclareSeasonS", strDeclareSeasonS);
                                    CmdP.Parameters.AddWithValue("@DeclareYearE", strDeclareYearE);
                                    CmdP.Parameters.AddWithValue("@DeclareMonthE", strDeclareMonthE);
                                    CmdP.Parameters.AddWithValue("@DeclareDayE", strDeclareDayE);
                                    CmdP.Parameters.AddWithValue("@DeclareSeasonE", strDeclareSeasonE);
                                    CmdP.Parameters.AddWithValue("@OriginalSN", strOriginalSN);

                                    try
                                    {
                                        CmdP.ExecuteNonQuery();
                                    }
                                    catch (Exception ex)
                                    {
                                        //插入轉置失敗Log。
                                        intErrorCount++;    //錯誤筆數+1
                                        funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strTempTableName, ex.Message, 
                                            "更新 801通關簽審資料 (TClearanceSignify801)：失敗。");
                                        continue;
                                    }
                                }
                                #endregion
                                #endregion

                                #endregion


                                #region 證件資料比對。

                                #region 證件參數設定

                                string strToxicNo = "-";
                                string strConcentration = dtTempDataResult.Rows[intCount]["Action1"].ToString() + "~" + dtTempDataResult.Rows[intCount]["Action2"].ToString();

                                string strPlace_No = "-";
                                string strCertificateApprNo = dtTempDataResult.Rows[intCount]["Doc_No"].ToString();

                                DateTime CertificateApprDate;
                                if (!DateTime.TryParse(dtTempDataResult.Rows[intCount]["A_D_Date"].ToString(), out CertificateApprDate))
                                {
                                    CertificateApprDate = Convert.ToDateTime("1900-01-01 00:00:00");
                                };

                                DateTime CertificateValidDate;
                                if (!DateTime.TryParse(dtTempDataResult.Rows[intCount]["Val_Date"].ToString(), out CertificateValidDate))
                                {
                                    CertificateValidDate = Convert.ToDateTime("1900-01-01 00:00:00");
                                };

                                string strApplicationInfo = "-";

                                blTWaterPollutionExam = false;    //判斷該筆資料的上下游資料是否存在。
                                #endregion

                                #region 因有做刪除，所以直接Insert證件
                                strCmd = @"INSERT INTO EPAChemiCredential(TempTableName, ChemicalChnName, ChemicalEngName, CASNo, ToxicNo, Concentration, 
                                            PercentageUnit, BusinessAdminNo, FactoryRegNo, EmsNo, CompanyName, CompanyAddress, Place_No, CertificateApprNo, 
                                            CertificateApprDate, CertificateValidDate, ApplicationInfo, UpdateDate, TransId, No) 
                                            VALUES (@TempTableName, @ChemicalChnName, @ChemicalEngName, @CASNo, @ToxicNo, @Concentration, 
                                            @PercentageUnit, @BusinessAdminNo, @FactoryRegNo, @EmsNo, @CompanyName, @CompanyAddress, @Place_No, @CertificateApprNo, 
                                            @CertificateApprDate, @CertificateValidDate, @ApplicationInfo, @UpdateDate, @TransId, @No) ";

                                CmdP.CommandText = strCmd;
                                CmdP.Parameters.Clear();
                                CmdP.Parameters.AddWithValue("@TempTableName", strTempTableName);
                                CmdP.Parameters.AddWithValue("@ChemicalChnName", strChemicalChnName);
                                CmdP.Parameters.AddWithValue("@ChemicalEngName", strChemicalEngName);
                                CmdP.Parameters.AddWithValue("@CASNo", strCASNo);
                                CmdP.Parameters.AddWithValue("@ToxicNo", strToxicNo);
                                CmdP.Parameters.AddWithValue("@Concentration", strConcentration);
                                CmdP.Parameters.AddWithValue("@PercentageUnit", "");
                                CmdP.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                                CmdP.Parameters.AddWithValue("@FactoryRegNo", strFactoryRegNo);
                                CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                                CmdP.Parameters.AddWithValue("@CompanyName", strComFacBizName);
                                CmdP.Parameters.AddWithValue("@CompanyAddress", strComFacBizAddr);
                                CmdP.Parameters.AddWithValue("@Place_No", strPlace_No);
                                CmdP.Parameters.AddWithValue("@CertificateApprNo", strCertificateApprNo);
                                CmdP.Parameters.AddWithValue("@CertificateApprDate", CertificateApprDate);
                                CmdP.Parameters.AddWithValue("@CertificateValidDate", CertificateValidDate);
                                CmdP.Parameters.AddWithValue("@ApplicationInfo", strApplicationInfo);
                                CmdP.Parameters.AddWithValue("@UpdateDate", strStartTime);
                                CmdP.Parameters.AddWithValue("@TransId", strTransId);
                                CmdP.Parameters.AddWithValue("@No", strNo);

                                try
                                {
                                    CmdP.ExecuteNonQuery();
                                }
                                catch (Exception ex)
                                {
                                    //插入轉置失敗Log。
                                    intErrorCount++;    //錯誤筆數+1
                                    funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strTempTableName, ex.Message, 
                                        "新增 801通關簽審資料 (TClearanceSignify801)：證件資料失敗。");
                                    //continue;
                                }
                                #endregion

                                #endregion

                                //目前此表無其他用途暫不處理
                                #region EPAClearanceSignify801表比對。

//                                //string strID = dtTempDataResult.Rows[intCount]["ID"].ToString();
//                                //string strTradeVan_Id = dtTempDataResult.Rows[intCount]["TradeVan_Id"].ToString();
//                                string strCityCode = dtTempDataResult.Rows[intCount]["CityCode"].ToString();
//                                string strMainId = dtTempDataResult.Rows[intCount]["MainId"].ToString();
//                                string strNu_CName = dtTempDataResult.Rows[intCount]["Nu_CName"].ToString();
//                                string strNu_EName = dtTempDataResult.Rows[intCount]["Nu_EName"].ToString();
//                                string strAction1 = dtTempDataResult.Rows[intCount]["Action1"].ToString();
//                                string strAction2 = dtTempDataResult.Rows[intCount]["Action2"].ToString();
//                                //string strCas_No = dtTempDataResult.Rows[intCount]["Cas_No"].ToString();
//                                string strUse_Goal = dtTempDataResult.Rows[intCount]["Use_Goal"].ToString();
//                                string strCccCode = dtTempDataResult.Rows[intCount]["CccCode"].ToString();
//                                string strICo_No = dtTempDataResult.Rows[intCount]["ICo_No"].ToString();
//                                //string strIName = dtTempDataResult.Rows[intCount]["IName"].ToString();
//                                //string strIAdd = dtTempDataResult.Rows[intCount]["IAdd"].ToString();
//                                string strIsNewChemist = dtTempDataResult.Rows[intCount]["IsNewChemist"].ToString();
//                                //string strQty = dtTempDataResult.Rows[intCount]["Qty"].ToString();
//                                string strQty_Unit = dtTempDataResult.Rows[intCount]["Qty_Unit"].ToString();
//                                string strCName = dtTempDataResult.Rows[intCount]["CName"].ToString();
//                                string strCAdd = dtTempDataResult.Rows[intCount]["CAdd"].ToString();
//                                string strComp_Id = dtTempDataResult.Rows[intCount]["Comp_Id"].ToString();
//                                string strBoss_Name = dtTempDataResult.Rows[intCount]["Boss_Name"].ToString();
//                                string strBoss_Id = dtTempDataResult.Rows[intCount]["Boss_Id"].ToString();
//                                string strConName = dtTempDataResult.Rows[intCount]["ConName"].ToString();
//                                string strCTel = dtTempDataResult.Rows[intCount]["CTel"].ToString();
//                                string strEMail = dtTempDataResult.Rows[intCount]["EMail"].ToString();
//                                string strDoc_No = dtTempDataResult.Rows[intCount]["Doc_No"].ToString();
//                                string strApply_Date = dtTempDataResult.Rows[intCount]["Apply_Date"].ToString();
//                                string strA_D_Date = dtTempDataResult.Rows[intCount]["A_D_Date"].ToString();
//                                string strVal_Date = dtTempDataResult.Rows[intCount]["Val_Date"].ToString();
//                                string strModifyDate = dtTempDataResult.Rows[intCount]["ModifyDate"].ToString();
//                                string strTradeType = dtTempDataResult.Rows[intCount]["TradeType"].ToString();
//                                string strOriginal_Name = dtTempDataResult.Rows[intCount]["Original_Name"].ToString();
//                                string strUse_Target = dtTempDataResult.Rows[intCount]["Use_Target"].ToString();
//                                string strUse_Range = dtTempDataResult.Rows[intCount]["Use_Range"].ToString();
//                                string strUse_Range_Other = dtTempDataResult.Rows[intCount]["Use_Range_Other"].ToString();
//                                string strSCo_No = dtTempDataResult.Rows[intCount]["SCo_No"].ToString();
//                                string strMajorConstituentCName = dtTempDataResult.Rows[intCount]["MajorConstituentCName"].ToString();
//                                string strMajorConstituentEName = dtTempDataResult.Rows[intCount]["MajorConstituentEName"].ToString();
//                                string strMajorConstituentOriginalName = dtTempDataResult.Rows[intCount]["MajorConstituentOriginalName"].ToString();
//                                string strSubConstituentOther = dtTempDataResult.Rows[intCount]["SubConstituentOther"].ToString();
//                                string strUse_GoalList = dtTempDataResult.Rows[intCount]["Use_GoalList"].ToString();
//                                string strUse_GoalListMain = dtTempDataResult.Rows[intCount]["Use_GoalListMain"].ToString();
//                                string strUse_TargetList = dtTempDataResult.Rows[intCount]["Use_TargetList"].ToString();
//                                string strCustomsDeclaration_date = dtTempDataResult.Rows[intCount]["CustomsDeclaration_date"].ToString();


//                                strCmd = @"INSERT INTO EPAClearanceSignify801 (ID, TradeVan_Id, CityCode, MainId, Nu_CName, Nu_EName, Action1, Action2, Cas_No, Use_Goal, CccCode, ICo_No, IName, IAdd, IsNewChemist, Qty, 
//                                                Qty_Unit, CName, CAdd, Comp_Id, Boss_Name, Boss_Id, ConName, CTel, EMail, Doc_No, Apply_Date, A_D_Date, Val_Date, ModifyDate, TradeType, 
//                                                Original_Name, Use_Target, Use_Range, Use_Range_Other, SCo_No, MajorConstituentCName, MajorConstituentEName, MajorConstituentOriginalName, 
//                                                SubConstituentOther, Use_GoalList, Use_GoalListMain, Use_TargetList, CustomsDeclaration_date, ComFacBizType, AdminNo) 
//                                                VALUES (@ID, @TradeVan_Id, @CityCode, @MainId, @Nu_CName, @Nu_EName, @Action1, @Action2, @Cas_No, @Use_Goal, @CccCode, @ICo_No, @IName, @IAdd, 
//                                                @IsNewChemist, @Qty, @Qty_Unit, @CName, @CAdd, @Comp_Id, @Boss_Name, @Boss_Id, @ConName, @CTel, @EMail, @Doc_No, @Apply_Date, @A_D_Date, 
//                                                @Val_Date, @ModifyDate, @TradeType, @Original_Name, @Use_Target, @Use_Range, @Use_Range_Other, @SCo_No, @MajorConstituentCName, 
//                                                @MajorConstituentEName, @MajorConstituentOriginalName, @SubConstituentOther, @Use_GoalList, @Use_GoalListMain, @Use_TargetList, 
//                                                @CustomsDeclaration_date, @ComFacBizType, @AdminNo) ";

//                                CmdP.CommandText = strCmd;
//                                CmdP.Parameters.Clear();
//                                CmdP.Parameters.AddWithValue("@ID", strID);
//                                CmdP.Parameters.AddWithValue("@TradeVan_Id", strTradeVan_Id);
//                                CmdP.Parameters.AddWithValue("@CityCode", strCityCode);
//                                CmdP.Parameters.AddWithValue("@MainId", strMainId);
//                                CmdP.Parameters.AddWithValue("@Nu_CName", strNu_CName);
//                                CmdP.Parameters.AddWithValue("@Nu_EName", strNu_EName);
//                                CmdP.Parameters.AddWithValue("@Action1", strAction1);
//                                CmdP.Parameters.AddWithValue("@Action2", strAction2);
//                                CmdP.Parameters.AddWithValue("@Cas_No", strCASNo);
//                                CmdP.Parameters.AddWithValue("@Use_Goal", strUse_Goal);
//                                CmdP.Parameters.AddWithValue("@CccCode", strCccCode);
//                                CmdP.Parameters.AddWithValue("@ICo_No", strICo_No);
//                                CmdP.Parameters.AddWithValue("@IName", strIName);
//                                CmdP.Parameters.AddWithValue("@IAdd", strIAdd);
//                                CmdP.Parameters.AddWithValue("@IsNewChemist", strIsNewChemist);
//                                CmdP.Parameters.AddWithValue("@Qty", strQty);
//                                CmdP.Parameters.AddWithValue("@Qty_Unit", strQty_Unit);
//                                CmdP.Parameters.AddWithValue("@CName", strCName);
//                                CmdP.Parameters.AddWithValue("@CAdd", strCAdd);
//                                CmdP.Parameters.AddWithValue("@Comp_Id", strComp_Id);
//                                CmdP.Parameters.AddWithValue("@Boss_Name", strBoss_Name);
//                                CmdP.Parameters.AddWithValue("@Boss_Id", strBoss_Id);
//                                CmdP.Parameters.AddWithValue("@ConName", strConName);
//                                CmdP.Parameters.AddWithValue("@CTel", strCTel);
//                                CmdP.Parameters.AddWithValue("@EMail", strEMail);
//                                CmdP.Parameters.AddWithValue("@Doc_No", strDoc_No);
//                                CmdP.Parameters.AddWithValue("@Apply_Date", strApply_Date);
//                                CmdP.Parameters.AddWithValue("@A_D_Date", strA_D_Date);
//                                CmdP.Parameters.AddWithValue("@Val_Date", strVal_Date);
//                                CmdP.Parameters.AddWithValue("@ModifyDate", strModifyDate);
//                                CmdP.Parameters.AddWithValue("@TradeType", strTradeType);
//                                CmdP.Parameters.AddWithValue("@Original_Name", strOriginal_Name);
//                                CmdP.Parameters.AddWithValue("@Use_Target", strUse_Target);
//                                CmdP.Parameters.AddWithValue("@Use_Range", strUse_Range);
//                                CmdP.Parameters.AddWithValue("@Use_Range_Other", strUse_Range_Other);
//                                CmdP.Parameters.AddWithValue("@SCo_No", strSCo_No);
//                                CmdP.Parameters.AddWithValue("@MajorConstituentCName", strMajorConstituentCName);
//                                CmdP.Parameters.AddWithValue("@MajorConstituentEName", strMajorConstituentEName);
//                                CmdP.Parameters.AddWithValue("@MajorConstituentOriginalName", strMajorConstituentOriginalName);
//                                CmdP.Parameters.AddWithValue("@SubConstituentOther", strSubConstituentOther);
//                                CmdP.Parameters.AddWithValue("@Use_GoalList", strUse_GoalList);
//                                CmdP.Parameters.AddWithValue("@Use_GoalListMain", strUse_GoalListMain);
//                                CmdP.Parameters.AddWithValue("@Use_TargetList", strUse_TargetList);
//                                CmdP.Parameters.AddWithValue("@CustomsDeclaration_date", strCustomsDeclaration_date);
//                                CmdP.Parameters.AddWithValue("@ComFacBizType", strComFacBizType);
//                                CmdP.Parameters.AddWithValue("@AdminNo", strAdminNo);


//                                try
//                                {
//                                    CmdP.ExecuteNonQuery();
//                                }
//                                catch (Exception ex)
//                                {
//                                    //插入轉置失敗Log。
//                                    intErrorCount++;    //錯誤筆數+1
//                                    funcUtil.ins1stDataTransErrorLog(strNo, strTransId,
//                                                         strTempTableName, ex.Message, "新增資料至EPAClearanceSignify801表失敗。");
//                                    //continue;
//                                }
                                #endregion

                            }
                            catch (Exception ex)
                            {
                                //塞入單筆轉置錯誤的訊息。
                                Console.WriteLine("第 " + dtTempDataResult.Rows[intCount]["No"].ToString() + " 筆轉置失敗。" + ex.Message);
                                funcUtil.ins1stDataTransErrorLog(dtTempDataResult.Rows[intCount]["No"].ToString(), strTransId, strTempTableName, ex.Message, "801通關簽審資料 (TClearanceSignify801)");
                                intErrorCount++;
                            }
                        }

                        //將暫存資料表的轉置狀態設置為已轉置。
                        funcUtil.updIsConvertedTrue(strTransId, strTempTableName);

                        funcUtil.updIsConvertedTrue(trID, "TProductING801");

                        //插入轉置完成log
                        string strEndTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); //取得轉置開始時間
                        funcUtil.ins1stDataTransLog(strTransId, "EPA000022",
                                                 strStartTime, strEndTime, strTempTableName,
                                                 intDataTotalCount, intErrorCount);
                    }


                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                funcUtil.ins1stDataTransErrorLog("first try-catch", strTransId, strTempTableName,
                    ex.Message, "801通關簽審資料 (TClearanceSignify801)");
            }
            finally
            {
                //關閉 Temp DB 連線。
                ConnT.Dispose();
                if (ConnT != null)
                    ConnT.Close();
                CmdT.Dispose();

                //關閉 Primary DB 連線。
                ConnP.Dispose();
                if (ConnP != null)
                    ConnP.Close();
                CmdP.Dispose();
            }
        }

        private static string transMonthToSeason(string month)
        {
            string season = "";
            if (Convert.ToInt16(month) >= 1 && Convert.ToInt16(month) <= 3)
            {
                season = "1";
            }
            else if (Convert.ToInt16(month) >= 4 && Convert.ToInt16(month) <= 6)
            {
                season = "2";
            }
            else if (Convert.ToInt16(month) >= 7 && Convert.ToInt16(month) <= 9)
            {
                season = "3";
            }
            else if (Convert.ToInt16(month) >= 10 && Convert.ToInt16(month) <= 12)
            {
                season = "4";
            }
            return season;
        }

        private static string getCAS(string cas) 
        {
            string rs = "",temp="",patn="";
            Regex rgx = new Regex(@"\d{2}-\d{2}-\d{1}");
            MatchCollection mc = rgx.Matches(cas);
            Match mch;
            for (int i = 0; i < mc.Count;i++ )
            {
                for (int j = 7; j >= 2; j--)//CAS第一部分有2到7位數字
                {
                    patn = @"\d{"+j+@"}-\d{2}-\d{1}";
                    mch = new Regex(patn).Match(cas);
                    rs = mch.ToString();
                    if (rs != "")
                    {
                        temp = temp + ";" + rs;
                        cas = cas.Replace(rs, "");
                        break;
                    }                    
                }
                if (i + 1 == mc.Count) 
                {
                    temp = temp.Substring(1);
                }
            }
            return temp;
        }
    }
}
