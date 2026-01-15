using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace Normalization_MS
{
    class ComFacBizMatch
    {
        public static string strPrimaryDBConn = ConfigurationManager.AppSettings["PrimaryDBConnectionStr"];     //ChemiPrimary DB 連限字串
        public static Dictionary<string, string> dicDepartmentMapping = new Dictionary<string, string>();

        /// <summary>
        /// 透過管制編號、統一編號及工廠登記編號去搜尋公司等大表，並回傳對應的結果(ComFacBizType,AdminNo)。
        /// </summary>
        /// <param name="strEmsNo">管制編號</param>
        /// <param name="strBusinessAdminNo">統一編號</param>
        /// <param name="strFactoryRegNo">工廠登記編號</param>
        /// <param name="strComFacBizName">公司工廠營利事業名稱(比對用)</param>
        /// <param name="strComFacBizAddr">地址(塞值用)</param>
        /// <returns>
        /// 回傳 ComFacBizType,AdminNo 組合字串。
        /// ComFacBizType：0-公司；1-工廠；2-營利事業；3-其他
        /// AdminNo：0-公司統一編號/1-工廠登記證號/2-營利事業統一編號/3-唯一值，識別用：TransId + No+(0-本身/1-上游/2-下游) 或 yyMMddHHmmssfff
        /// </returns>
        public static string MainFunc(string strEmsNo, string strBusinessAdminNo, string strFactoryRegNo,
                                      string strComFacBizName, string strComFacBizAddr,
                                      string strTransId, string strSourseTableName, string strNo)
        {
            #region 取得 TempTableName 對應的部會別代碼(EX: MOEA、EPA、COA...)
            string strDepId = "";
            try
            {
                if (strSourseTableName == "FactoryMap") // 由於FactoryMap非CDX來源資料,故設定例外條件,以進行此程式
                    strDepId = "EPA";
                else if (strSourseTableName == "BaseData") // 由於BaseData非CDX來源資料,故設定例外條件,以進行此程式
                    strDepId = "EPA";
                else if (strSourseTableName == "StoreData") // 由於StoreData非CDX來源資料,故設定例外條件,以進行此程式
                    strDepId = "EPA";
                else if (strSourseTableName == "Disaster_Basic") // 由於Disaster_Basic非CDX來源資料,故設定例外條件,以進行此程式
                    strDepId = "EPA";
                else
                    strDepId = dicDepartmentMapping[strSourseTableName];
            }
            catch (Exception ex)
            {
                //若發生錯誤則需要將錯誤訊息新增至 DataTransErrorLog。
                funcUtil.ins1stDataTransErrorLog("-", "-", "-", ex.Message, strSourseTableName);
                Console.WriteLine("ComFacBizMatch.dicDepartmentMapping錯誤(strDepId)，" + ex.Message);
            }
            #endregion

            string result = string.Empty;   //回傳結果(ComFacBizType,AdminNo)

            var strComFacBizType = string.Empty;          //暫存 ComFacBizType (0-公司；1-工廠；2-營利事業；3-其他)
            var strAdminNo = "-";                         //暫存 AdminNo (0-公司統一編號 / 1-工廠登記證號/ 2-營利事業統一編號/ 3-唯一值，識別用：遞增流水號)
            var strComFacBizAddrPK = "";                  //暫存長度大於 255 的地址的 PK。
            string strEPACompanyName = string.Empty;      //暫存從 EPAComFacBizInfo 抓到的 CompanyName
            string strFactoryAddressOrig = string.Empty;  //暫存從 EPAComFacBizInfo 抓到的 FactoryAddress(原始)
            string strFactoryAddress = string.Empty;      //暫存從 EPAComFacBizInfo 抓到的 FactoryAddress(正規化)
            bool SourceExist = false;                     //暫存判斷資料搜尋後是否存在
            bool Exist = false;                           //暫存判斷資料搜尋後是否存在

            // 註：strEmsNo != strSourseTableName 判斷用於客製化轉置需求，若將 EmsNo 給定 SourseTableName 則進行客製化轉置。
            if (strEmsNo != strSourseTableName)
            {
                #region Step 0：傳入參數去除空白字元與補上 Primary Key 為空值須修正為「-」

                if (string.IsNullOrEmpty(strEmsNo) || string.IsNullOrWhiteSpace(strEmsNo))
                    strEmsNo = "-";
                else
                    strEmsNo = strEmsNo.Trim();

                if (string.IsNullOrEmpty(strBusinessAdminNo) || string.IsNullOrWhiteSpace(strBusinessAdminNo))
                    strBusinessAdminNo = "-";
                else
                    strBusinessAdminNo = strBusinessAdminNo.Trim();

                if (string.IsNullOrEmpty(strFactoryRegNo) || string.IsNullOrWhiteSpace(strFactoryRegNo))
                    strFactoryRegNo = "-";
                else
                    strFactoryRegNo = strFactoryRegNo.Trim();

                if (string.IsNullOrEmpty(strComFacBizName) || string.IsNullOrWhiteSpace(strComFacBizName))
                    strComFacBizName = "-";
                else
                    strComFacBizName = strComFacBizName.Trim();

                if (string.IsNullOrEmpty(strComFacBizAddr))
                {
                    strComFacBizAddr = "-";
                    strComFacBizAddrPK = "-";
                }
                else
                {
                    strComFacBizAddr = strComFacBizAddr.Trim();
                    if (strComFacBizAddr.Trim().Length > 250)
                        strComFacBizAddrPK = strComFacBizAddr.Trim().Substring(strComFacBizAddr.Trim().Length - 250);
                    else
                        strComFacBizAddrPK = strComFacBizAddr.Trim();
                }

                string strComFacBizNameIndex = "";
                if (strComFacBizName.Length > 1)
                    strComFacBizNameIndex = strComFacBizName.Substring(0, 1);
                else
                    strComFacBizNameIndex = strComFacBizName;

                #endregion

                #region Step 1：先搜尋 SourceComData 是否存在該筆資料，存在則直接回傳該筆 ComFacBizType 及 AdminNo。

                try
                {
                    SqlConnection Conn = new SqlConnection(strPrimaryDBConn);
                    SqlCommand Cmd = Conn.CreateCommand();
                    Conn.Open();
                    Cmd.CommandTimeout = 3000;

                    Cmd.CommandText = "SELECT ComFacBizType, AdminNo, EmsNo, BusinessAdminNo, FactoryRegNo, SourceComName, CompanyAddress, TempTableName, IsComMatched, OldAdminNo FROM SourceComData" + strDepId + 
                                    " WHERE EmsNo=@EmsNo AND BusinessAdminNo=@BusinessAdminNo AND FactoryRegNo=@FactoryRegNo AND SourceComName=@SourceComName AND CompanyAddress=@CompanyAddress AND TempTableName=@TempTableName";
                    Cmd.Parameters.Clear();
                    Cmd.Parameters.AddWithValue("@EmsNo", strEmsNo);
                    Cmd.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                    Cmd.Parameters.AddWithValue("@FactoryRegNo", strFactoryRegNo);
                    Cmd.Parameters.AddWithValue("@SourceComName", strComFacBizName);
                    Cmd.Parameters.AddWithValue("@CompanyAddress", strComFacBizAddrPK);
                    Cmd.Parameters.AddWithValue("@TempTableName", strSourseTableName);
                    SqlDataReader drSource = Cmd.ExecuteReader();
                    if (drSource.Read())
                    {
                        SourceExist = true;
                        strComFacBizType = drSource["ComFacBizType"].ToString();
                        strAdminNo = drSource["AdminNo"].ToString();

                        if (drSource["IsComMatched"].ToString() == "True")
                        {
                            strComFacBizType = "3";
                            strAdminNo = drSource["OldAdminNo"].ToString();
                        }

                    }
                    if (drSource != null)
                        drSource.Close();
                    if (SourceExist)
                        result = strComFacBizType + "," + strAdminNo;

                    Conn.Dispose();
                    if (Conn != null)
                        Conn.Close();
                    Cmd.Dispose();
                }
                catch (Exception ex)
                {
                    //若發生錯誤則需要將錯誤訊息新增至 DataTransErrorLog。
                    funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strSourseTableName, ex.Message, "ComFacBizMatch.SourceComData");
                    Console.WriteLine("ComFacBizMatch.SourceComData錯誤，" + ex.Message);
                }

                if (SourceExist)
                    return result;

                #endregion

                #region  Step Main：

                try
                {
                    //因為需要從 Primary DB 取出轉置的資料，需要定義 Primary DB 連線。
                    SqlConnection ConnP = new SqlConnection(strPrimaryDBConn);
                    SqlCommand CmdP = ConnP.CreateCommand();
                    ConnP.Open();
                    CmdP.CommandTimeout = 3000;

                    #region Step 2：判定來源資料是否包含管制編號(EmsNo)，有管編的廠商「大部分」應歸類至工廠(FactoryInfo)。

                    if (strEmsNo != string.Empty && strEmsNo != "-")
                    {
                        #region Step 2-0：用 EmsNo 去搜尋 EPAComFacBizInfo 取得名稱(環保署廠商基本資料表名稱)、地址(環保署廠商基本資料表地址)。

                        bool blEmsData = false;     //判斷該 EmsNo 是否存在於 EPAComFacBizInfo。
                        CmdP.CommandText = "SELECT TOP 1 CompanyName, FactoryAddress FROM EPAComFacBizInfo WHERE EmsNo=@EmsNo";
                        CmdP.Parameters.Clear();
                        CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                        SqlDataReader drEPAComFacBizInfo = CmdP.ExecuteReader();
                        if (drEPAComFacBizInfo.Read())
                        {
                            blEmsData = true;

                            strEPACompanyName = drEPAComFacBizInfo["CompanyName"].ToString();
                            if (string.IsNullOrEmpty(strEPACompanyName))
                                strEPACompanyName = "-";
                            else
                                strEPACompanyName = strEPACompanyName.Trim();

                            strFactoryAddressOrig = drEPAComFacBizInfo["FactoryAddress"].ToString();
                            if (string.IsNullOrEmpty(strFactoryAddressOrig))
                                strFactoryAddressOrig = "-";
                            else
                                strFactoryAddressOrig = strFactoryAddressOrig.Trim();

                            strFactoryAddress = AddressAlignment(strFactoryAddressOrig);
                            if (string.IsNullOrEmpty(strFactoryAddress))
                                strFactoryAddress = "-";
                            else
                                strFactoryAddress = strFactoryAddress.Trim();
                        }
                        if (drEPAComFacBizInfo != null)
                            drEPAComFacBizInfo.Close();

                        #endregion

                        #region Step 2-1：用「名稱」、「環保署廠商基本資料表地址」搜尋 FactoryInfo、CompanyInfo、BusinessInfo、ComFacBizInfo。

                        #region Step 2-1-1：名稱與地址搜尋 FactoryInfo。

                        if (strComFacBizName != string.Empty && strComFacBizName != "-" && blEmsData && strFactoryAddress != "-" && Exist == false)
                        {
                            CmdP.CommandText = "SELECT FactoryRegNo, FactoryAddressComb, EmsNo FROM FactoryInfo WHERE FactoryNameIndex=@FactoryNameIndex AND FactoryName=@FactoryName ORDER BY FactoryRegNo";
                            CmdP.Parameters.Clear();
                            CmdP.Parameters.AddWithValue("@FactoryNameIndex", strComFacBizNameIndex);
                            CmdP.Parameters.AddWithValue("@FactoryName", strComFacBizName);
                            SqlDataAdapter daFactoryInfo = new SqlDataAdapter(CmdP);
                            DataTable dtFactoryInfo = new DataTable();
                            daFactoryInfo.Fill(dtFactoryInfo);
                            for (int i = 0; i < dtFactoryInfo.Rows.Count; i++)
                            {
                                string strEmsNoTemp = string.Empty;

                                string strAddr = AddressAlignment(dtFactoryInfo.Rows[i]["FactoryAddressComb"].ToString());
                                if (!string.IsNullOrEmpty(strAddr) && strFactoryAddress.Equals(strAddr))
                                {
                                    Exist = true;
                                    strComFacBizType = "1";
                                    strAdminNo = dtFactoryInfo.Rows[i]["FactoryRegNo"].ToString();
                                    strEmsNoTemp = dtFactoryInfo.Rows[i]["EmsNo"].ToString();
                                }

                                if (Exist && strEmsNo != string.Empty && strEmsNo != "-")
                                {
                                    if (strEmsNoTemp == "-" || strEmsNoTemp == "" || strEmsNoTemp != strEmsNo)
                                    {
                                        CmdP.CommandText = "UPDATE FactoryInfo SET EmsNo=@EmsNo WHERE FactoryRegNo=@FactoryRegNo";
                                        CmdP.Parameters.Clear();
                                        CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                                        CmdP.Parameters.AddWithValue("@FactoryRegNo", strAdminNo);
                                        CmdP.ExecuteNonQuery();
                                    }
                                }

                                if (Exist)
                                    break;
                            }
                        }

                        #endregion

                        #region Step 2-1-2：名稱與地址搜尋 CompanyInfo。

                        if (strComFacBizName != string.Empty && strComFacBizName != "-" && blEmsData && strFactoryAddress != "-" && Exist == false)
                        {
                            CmdP.CommandText = "SELECT T1.BusinessAdminNo, CONCAT(T2.ZipLocalName, T1.CompanyAddress) AS Addr, T1.EmsNo FROM CompanyInfo AS T1 INNER JOIN CountyZipMapping AS T2 ON T1.CompanyZipCode = T2.ZipCode WHERE T1.CompanyNameIndex=@CompanyNameIndex AND T1.CompanyName=@CompanyName ORDER BY T1.BusinessAdminNo";
                            CmdP.Parameters.Clear();
                            CmdP.Parameters.AddWithValue("@CompanyNameIndex", strComFacBizNameIndex);
                            CmdP.Parameters.AddWithValue("@CompanyName", strComFacBizName);
                            SqlDataAdapter daCompanyInfo = new SqlDataAdapter(CmdP);
                            DataTable dtCompanyInfo = new DataTable();
                            daCompanyInfo.Fill(dtCompanyInfo);
                            for (int i = 0; i < dtCompanyInfo.Rows.Count; i++)
                            {
                                string strEmsNoTemp = string.Empty;

                                string strAddr = AddressAlignment(dtCompanyInfo.Rows[i]["Addr"].ToString());
                                if (!string.IsNullOrEmpty(strAddr) && strFactoryAddress.Equals(strAddr))
                                {
                                    Exist = true;
                                    strComFacBizType = "0";
                                    strAdminNo = dtCompanyInfo.Rows[i]["BusinessAdminNo"].ToString();
                                    strEmsNoTemp = dtCompanyInfo.Rows[i]["EmsNo"].ToString();
                                }

                                if (Exist && strEmsNo != string.Empty && strEmsNo != "-")
                                {
                                    if (strEmsNoTemp == "-" || strEmsNoTemp == "" || strEmsNoTemp != strEmsNo)
                                    {
                                        CmdP.CommandText = "UPDATE CompanyInfo SET EmsNo=@EmsNo WHERE BusinessAdminNo=@BusinessAdminNo";
                                        CmdP.Parameters.Clear();
                                        CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                                        CmdP.Parameters.AddWithValue("@BusinessAdminNo", strAdminNo);
                                        CmdP.ExecuteNonQuery();
                                    }
                                }

                                if (Exist)
                                    break;
                            }
                        }

                        #endregion

                        #region Step 2-1-3：名稱與地址搜尋 BusinessInfo。

                        if (strComFacBizName != string.Empty && strComFacBizName != "-" && blEmsData && strFactoryAddress != "-" && Exist == false)
                        {
                            CmdP.CommandText = "SELECT T1.BusinessAdminNo, CONCAT(T2.ZipLocalName, T1.BusinessAddress) AS Addr, T1.EmsNo FROM BusinessInfo AS T1 INNER JOIN CountyZipMapping AS T2 ON T1.BusinessZipCode = T2.ZipCode WHERE T1.BusinessNameIndex=@BusinessNameIndex AND T1.BusinessName=@BusinessName ORDER BY T1.BusinessAdminNo";
                            CmdP.Parameters.Clear();
                            CmdP.Parameters.AddWithValue("@BusinessNameIndex", strComFacBizNameIndex);
                            CmdP.Parameters.AddWithValue("@BusinessName", strComFacBizName);
                            SqlDataAdapter daBusinessInfo = new SqlDataAdapter(CmdP);
                            DataTable dtBusinessInfo = new DataTable();
                            daBusinessInfo.Fill(dtBusinessInfo);
                            for (int i = 0; i < dtBusinessInfo.Rows.Count; i++)
                            {
                                string strEmsNoTemp = string.Empty;

                                string strAddr = AddressAlignment(dtBusinessInfo.Rows[i]["Addr"].ToString());
                                if (!string.IsNullOrEmpty(strAddr) && strFactoryAddress.Equals(strAddr))
                                {
                                    Exist = true;
                                    strComFacBizType = "2";
                                    strAdminNo = dtBusinessInfo.Rows[i]["BusinessAdminNo"].ToString();
                                    strEmsNoTemp = dtBusinessInfo.Rows[i]["EmsNo"].ToString();
                                }

                                if (Exist && strEmsNo != string.Empty && strEmsNo != "-")
                                {
                                    if (strEmsNoTemp == "-" || strEmsNoTemp == "" || strEmsNoTemp != strEmsNo)
                                    {
                                        CmdP.CommandText = "UPDATE BusinessInfo SET EmsNo=@EmsNo WHERE BusinessAdminNo=@BusinessAdminNo";
                                        CmdP.Parameters.Clear();
                                        CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                                        CmdP.Parameters.AddWithValue("@BusinessAdminNo", strAdminNo);
                                        CmdP.ExecuteNonQuery();
                                    }
                                }

                                if (Exist)
                                    break;
                            }
                        }

                        #endregion

                        #region Step 2-1-4：名稱與地址搜尋 ComFacBizInfo。

                        if (strComFacBizName != string.Empty && strComFacBizName != "-" && blEmsData && strFactoryAddress != "-" && Exist == false)
                        {
                            CmdP.CommandText = "SELECT AdminNo, ComFacBizAddr, EmsNo FROM ComFacBizInfo WHERE ComFacBizNameIndex=@ComFacBizNameIndex AND ComFacBizName=@ComFacBizName ORDER BY AdminNo";
                            CmdP.Parameters.Clear();
                            CmdP.Parameters.AddWithValue("@ComFacBizNameIndex", strComFacBizNameIndex);
                            CmdP.Parameters.AddWithValue("@ComFacBizName", strComFacBizName);
                            SqlDataAdapter daComFacBizInfo = new SqlDataAdapter(CmdP);
                            DataTable dtComFacBizInfo = new DataTable();
                            daComFacBizInfo.Fill(dtComFacBizInfo);
                            for (int i = 0; i < dtComFacBizInfo.Rows.Count; i++)
                            {
                                string strEmsNoTemp = string.Empty;

                                string strAddr = AddressAlignment(dtComFacBizInfo.Rows[i]["ComFacBizAddr"].ToString());
                                if (!string.IsNullOrEmpty(strAddr) && strFactoryAddress.Equals(strAddr))
                                {
                                    Exist = true;
                                    strComFacBizType = "3";
                                    strAdminNo = dtComFacBizInfo.Rows[i]["AdminNo"].ToString();
                                    strEmsNoTemp = dtComFacBizInfo.Rows[i]["EmsNo"].ToString();
                                }

                                if (Exist && strEmsNo != string.Empty && strEmsNo != "-")
                                {
                                    if (strEmsNoTemp == "-" || strEmsNoTemp == "" || strEmsNoTemp != strEmsNo)
                                    {
                                        CmdP.CommandText = "UPDATE ComFacBizInfo SET EmsNo=@EmsNo WHERE AdminNo=@AdminNo";
                                        CmdP.Parameters.Clear();
                                        CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                                        CmdP.Parameters.AddWithValue("@AdminNo", strAdminNo);
                                        CmdP.ExecuteNonQuery();
                                    }
                                }

                                if (Exist)
                                    break;
                            }
                        }

                        #endregion

                        #endregion

                        #region Step 2-2：若 Step 2-1 搜尋不到且有廠編，則用「廠編」、「環保署廠商基本資料表地址」搜尋 FactoryInfo、ComFacBizInfo。

                        #region Step 2-2-1：廠編與地址搜尋 FactoryInfo。

                        if (strFactoryRegNo != string.Empty && strFactoryRegNo != "-" && blEmsData && strFactoryAddress != "-" && Exist == false)
                        {
                            CmdP.CommandText = "SELECT TOP 1 FactoryRegNo, FactoryAddressComb, EmsNo FROM FactoryInfo WHERE FactoryRegNo=@FactoryRegNo";
                            CmdP.Parameters.Clear();
                            CmdP.Parameters.AddWithValue("@FactoryRegNo", strFactoryRegNo);
                            SqlDataReader drFactoryInfo = CmdP.ExecuteReader();

                            string strEmsNoTemp = string.Empty;

                            if (drFactoryInfo.Read())
                            {
                                string strAddr = AddressAlignment(drFactoryInfo["FactoryAddressComb"].ToString());
                                if (!string.IsNullOrEmpty(strAddr) && strFactoryAddress.Equals(strAddr))
                                {
                                    Exist = true;
                                    strComFacBizType = "1";
                                    strAdminNo = strFactoryRegNo;
                                    strEmsNoTemp = drFactoryInfo["EmsNo"].ToString();
                                }
                            }
                            if (drFactoryInfo != null)
                                drFactoryInfo.Close();

                            if (Exist && strEmsNo != string.Empty && strEmsNo != "-")
                            {
                                if (strEmsNoTemp == "-" || strEmsNoTemp == "" || strEmsNoTemp != strEmsNo)
                                {
                                    CmdP.CommandText = "UPDATE FactoryInfo SET EmsNo=@EmsNo WHERE FactoryRegNo=@FactoryRegNo";
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                                    CmdP.Parameters.AddWithValue("@FactoryRegNo", strFactoryRegNo);
                                    CmdP.ExecuteNonQuery();
                                }
                            }
                        }

                        #endregion

                        #region Step 2-2-2：廠編與地址搜尋 ComFacBizInfo。

                        if (strFactoryRegNo != string.Empty && strFactoryRegNo != "-" && blEmsData && strFactoryAddress != "-" && Exist == false)
                        {
                            CmdP.CommandText = "SELECT AdminNo, ComFacBizAddr, EmsNo FROM ComFacBizInfo WHERE FactoryRegNo=@FactoryRegNo ORDER BY AdminNo";
                            CmdP.Parameters.Clear();
                            CmdP.Parameters.AddWithValue("@FactoryRegNo", strFactoryRegNo);
                            SqlDataAdapter daComFacBizInfo = new SqlDataAdapter(CmdP);
                            DataTable dtComFacBizInfo = new DataTable();
                            daComFacBizInfo.Fill(dtComFacBizInfo);
                            for (int i = 0; i < dtComFacBizInfo.Rows.Count; i++)
                            {
                                string strEmsNoTemp = string.Empty;

                                string strAddr = AddressAlignment(dtComFacBizInfo.Rows[i]["ComFacBizAddr"].ToString());
                                if (!string.IsNullOrEmpty(strAddr) && strFactoryAddress.Equals(strAddr))
                                {
                                    Exist = true;
                                    strComFacBizType = "3";
                                    strAdminNo = dtComFacBizInfo.Rows[i]["AdminNo"].ToString();
                                    strEmsNoTemp = dtComFacBizInfo.Rows[i]["EmsNo"].ToString();
                                }

                                if (Exist && strEmsNo != string.Empty && strEmsNo != "-")
                                {
                                    if (strEmsNoTemp == "-" || strEmsNoTemp == "" || strEmsNoTemp != strEmsNo)
                                    {
                                        CmdP.CommandText = "UPDATE ComFacBizInfo SET EmsNo=@EmsNo WHERE AdminNo=@AdminNo";
                                        CmdP.Parameters.Clear();
                                        CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                                        CmdP.Parameters.AddWithValue("@AdminNo", strAdminNo);
                                        CmdP.ExecuteNonQuery();
                                    }
                                }

                                if (Exist)
                                    break;
                            }
                        }

                        #endregion

                        #endregion

                        #region Step 2-3：若 Step 2-1 及 Step 2-2 搜尋不到且有統編，則用「統編」、「環保署廠商基本資料表地址」搜尋 CompanyInfo、BusinessInfo、ComFacBizInfo。

                        #region Step 2-3-1：統編與地址搜尋 CompanyInfo。

                        if (strBusinessAdminNo != string.Empty && strBusinessAdminNo != "-" && blEmsData && strFactoryAddress != "-" && Exist == false)
                        {
                            CmdP.CommandText = "SELECT TOP 1 T1.BusinessAdminNo, CONCAT(T2.ZipLocalName, T1.CompanyAddress) AS Addr, T1.EmsNo FROM CompanyInfo AS T1 INNER JOIN CountyZipMapping AS T2 ON T1.CompanyZipCode = T2.ZipCode WHERE T1.BusinessAdminNo=@BusinessAdminNo";
                            CmdP.Parameters.Clear();
                            CmdP.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                            SqlDataReader drCompanyInfo = CmdP.ExecuteReader();

                            string strEmsNoTemp = string.Empty;

                            if (drCompanyInfo.Read())
                            {
                                string strAddr = AddressAlignment(drCompanyInfo["Addr"].ToString());
                                if (!string.IsNullOrEmpty(strAddr) && strFactoryAddress.Equals(strAddr))
                                {
                                    Exist = true;
                                    strComFacBizType = "0";
                                    strAdminNo = strBusinessAdminNo;
                                    strEmsNoTemp = drCompanyInfo["EmsNo"].ToString();
                                }
                            }
                            if (drCompanyInfo != null)
                                drCompanyInfo.Close();

                            if (Exist && strEmsNo != string.Empty && strEmsNo != "-")
                            {
                                if (strEmsNoTemp == "-" || strEmsNoTemp == "" || strEmsNoTemp != strEmsNo)
                                {
                                    CmdP.CommandText = "UPDATE CompanyInfo SET EmsNo=@EmsNo WHERE BusinessAdminNo=@BusinessAdminNo";
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                                    CmdP.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                                    CmdP.ExecuteNonQuery();
                                }
                            }
                        }

                        #endregion

                        #region Step 2-3-2：統編與地址搜尋 BusinessInfo。

                        if (strBusinessAdminNo != string.Empty && strBusinessAdminNo != "-" && blEmsData && strFactoryAddress != "-" && Exist == false)
                        {
                            CmdP.CommandText = "SELECT TOP 1 T1.BusinessAdminNo, CONCAT(T2.ZipLocalName, T1.BusinessAddress) AS Addr, T1.EmsNo FROM BusinessInfo AS T1 INNER JOIN CountyZipMapping AS T2 ON T1.BusinessZipCode = T2.ZipCode WHERE T1.BusinessAdminNo=@BusinessAdminNo";
                            CmdP.Parameters.Clear();
                            CmdP.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                            SqlDataReader drBusinessInfo = CmdP.ExecuteReader();

                            string strEmsNoTemp = string.Empty;

                            if (drBusinessInfo.Read())
                            {
                                string strAddr = AddressAlignment(drBusinessInfo["Addr"].ToString());
                                if (!string.IsNullOrEmpty(strAddr) && strFactoryAddress.Equals(strAddr))
                                {
                                    Exist = true;
                                    strComFacBizType = "2";
                                    strAdminNo = strBusinessAdminNo;
                                    strEmsNoTemp = drBusinessInfo["EmsNo"].ToString();
                                }
                            }
                            if (drBusinessInfo != null)
                                drBusinessInfo.Close();

                            if (Exist && strEmsNo != string.Empty && strEmsNo != "-")
                            {
                                if (strEmsNoTemp == "-" || strEmsNoTemp == "" || strEmsNoTemp != strEmsNo)
                                {
                                    CmdP.CommandText = "UPDATE BusinessInfo SET EmsNo=@EmsNo WHERE BusinessAdminNo=@BusinessAdminNo";
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                                    CmdP.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                                    CmdP.ExecuteNonQuery();
                                }
                            }
                        }

                        #endregion

                        #region Step 2-3-3：統編與地址搜尋 ComFacBizInfo。

                        if (strBusinessAdminNo != string.Empty && strBusinessAdminNo != "-" && blEmsData && strFactoryAddress != "-" && Exist == false)
                        {
                            CmdP.CommandText = "SELECT AdminNo, ComFacBizAddr, EmsNo FROM ComFacBizInfo WHERE BusinessAdminNo=@BusinessAdminNo ORDER BY AdminNo";
                            CmdP.Parameters.Clear();
                            CmdP.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                            SqlDataAdapter daComFacBizInfo = new SqlDataAdapter(CmdP);
                            DataTable dtComFacBizInfo = new DataTable();
                            daComFacBizInfo.Fill(dtComFacBizInfo);
                            for (int i = 0; i < dtComFacBizInfo.Rows.Count; i++)
                            {
                                string strEmsNoTemp = string.Empty;

                                string strAddr = AddressAlignment(dtComFacBizInfo.Rows[i]["ComFacBizAddr"].ToString());
                                if (!string.IsNullOrEmpty(strAddr) && strFactoryAddress.Equals(strAddr))
                                {
                                    Exist = true;
                                    strComFacBizType = "3";
                                    strAdminNo = dtComFacBizInfo.Rows[i]["AdminNo"].ToString();
                                    strEmsNoTemp = dtComFacBizInfo.Rows[i]["EmsNo"].ToString();
                                }

                                if (Exist && strEmsNo != string.Empty && strEmsNo != "-")
                                {
                                    if (strEmsNoTemp == "-" || strEmsNoTemp == "" || strEmsNoTemp != strEmsNo)
                                    {
                                        CmdP.CommandText = "UPDATE ComFacBizInfo SET EmsNo=@EmsNo WHERE AdminNo=@AdminNo";
                                        CmdP.Parameters.Clear();
                                        CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                                        CmdP.Parameters.AddWithValue("@AdminNo", strAdminNo);
                                        CmdP.ExecuteNonQuery();
                                    }
                                }

                                if (Exist)
                                    break;
                            }
                        }

                        #endregion

                        #endregion

                        #region Step 2-4：若 Step 2-1 至 Step 2-3 都搜不到，則用「名稱」、「地址」搜尋 FactoryInfo、CompanyInfo、BusinessInfo、ComFacBizInfo。

                        #region Step 2-4-1：名稱與地址搜尋 FactoryInfo。

                        if (strComFacBizName != string.Empty && strComFacBizName != "-" && strComFacBizAddr != string.Empty && strComFacBizAddr != "-" && Exist == false)
                        {
                            CmdP.CommandText = "SELECT TOP 1 FactoryRegNo, EmsNo FROM FactoryInfo WHERE FactoryNameIndex=@FactoryNameIndex AND FactoryName=@FactoryName AND FactoryAddressComb=@FactoryAddressComb ORDER BY FactoryRegNo";
                            CmdP.Parameters.Clear();
                            CmdP.Parameters.AddWithValue("@FactoryNameIndex", strComFacBizNameIndex);
                            CmdP.Parameters.AddWithValue("@FactoryName", strComFacBizName);
                            CmdP.Parameters.AddWithValue("@FactoryAddressComb", strComFacBizAddr);
                            SqlDataReader drFactoryInfo = CmdP.ExecuteReader();

                            string strEmsNoTemp = string.Empty;

                            if (drFactoryInfo.Read())
                            {
                                Exist = true;
                                strComFacBizType = "1";
                                strAdminNo = drFactoryInfo["FactoryRegNo"].ToString();
                                strEmsNoTemp = drFactoryInfo["EmsNo"].ToString();
                            }
                            if (drFactoryInfo != null)
                                drFactoryInfo.Close();

                            if (Exist && strEmsNo != string.Empty && strEmsNo != "-")
                            {
                                if (strEmsNoTemp == "-" || strEmsNoTemp == "" || strEmsNoTemp != strEmsNo)
                                {
                                    CmdP.CommandText = "UPDATE FactoryInfo SET EmsNo=@EmsNo WHERE FactoryRegNo=@FactoryRegNo";
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                                    CmdP.Parameters.AddWithValue("@FactoryRegNo", strAdminNo);
                                    CmdP.ExecuteNonQuery();
                                }
                            }
                        }

                        #endregion

                        #region Step 2-4-2：名稱與地址搜尋 CompanyInfo。

                        if (strComFacBizName != string.Empty && strComFacBizName != "-" && strComFacBizAddr != string.Empty && strComFacBizAddr != "-" && Exist == false)
                        {
                            CmdP.CommandText = "SELECT TOP 1 T1.BusinessAdminNo, T1.EmsNo FROM CompanyInfo AS T1 INNER JOIN CountyZipMapping AS T2 ON T1.CompanyZipCode = T2.ZipCode WHERE T1.CompanyNameIndex=@CompanyNameIndex AND T1.CompanyName=@CompanyName AND CONCAT(T2.ZipLocalName, T1.CompanyAddress)=@CompanyAddress ORDER BY T1.BusinessAdminNo";
                            CmdP.Parameters.Clear();
                            CmdP.Parameters.AddWithValue("@CompanyNameIndex", strComFacBizNameIndex);
                            CmdP.Parameters.AddWithValue("@CompanyName", strComFacBizName);
                            CmdP.Parameters.AddWithValue("@CompanyAddress", strComFacBizAddr);
                            SqlDataReader drCompanyInfo = CmdP.ExecuteReader();

                            string strEmsNoTemp = string.Empty;

                            if (drCompanyInfo.Read())
                            {
                                Exist = true;
                                strComFacBizType = "0";
                                strAdminNo = drCompanyInfo["BusinessAdminNo"].ToString();
                                strEmsNoTemp = drCompanyInfo["EmsNo"].ToString();
                            }
                            if (drCompanyInfo != null)
                                drCompanyInfo.Close();

                            if (Exist && strEmsNo != string.Empty && strEmsNo != "-")
                            {
                                if (strEmsNoTemp == "-" || strEmsNoTemp == "" || strEmsNoTemp != strEmsNo)
                                {
                                    CmdP.CommandText = "UPDATE CompanyInfo SET EmsNo=@EmsNo WHERE BusinessAdminNo=@BusinessAdminNo";
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                                    CmdP.Parameters.AddWithValue("@BusinessAdminNo", strAdminNo);
                                    CmdP.ExecuteNonQuery();
                                }
                            }
                        }

                        #endregion

                        #region Step 2-4-3：名稱與地址搜尋 BusinessInfo。

                        if (strComFacBizName != string.Empty && strComFacBizName != "-" && strComFacBizAddr != string.Empty && strComFacBizAddr != "-" && Exist == false)
                        {
                            CmdP.CommandText = "SELECT TOP 1 T1.BusinessAdminNo, T1.EmsNo FROM BusinessInfo AS T1 INNER JOIN CountyZipMapping AS T2 ON T1.BusinessZipCode = T2.ZipCode WHERE T1.BusinessNameIndex=@BusinessNameIndex AND T1.BusinessName=@BusinessName AND CONCAT(T2.ZipLocalName, T1.BusinessAddress)=@BusinessAddress ORDER BY T1.BusinessAdminNo";
                            CmdP.Parameters.Clear();
                            CmdP.Parameters.AddWithValue("@BusinessNameIndex", strComFacBizNameIndex);
                            CmdP.Parameters.AddWithValue("@BusinessName", strComFacBizName);
                            CmdP.Parameters.AddWithValue("@BusinessAddress", strComFacBizAddr);
                            SqlDataReader drBusinessInfo = CmdP.ExecuteReader();

                            string strEmsNoTemp = string.Empty;

                            if (drBusinessInfo.Read())
                            {
                                Exist = true;
                                strComFacBizType = "2";
                                strAdminNo = drBusinessInfo["BusinessAdminNo"].ToString();
                                strEmsNoTemp = drBusinessInfo["EmsNo"].ToString();
                            }
                            if (drBusinessInfo != null)
                                drBusinessInfo.Close();

                            if (Exist && strEmsNo != string.Empty && strEmsNo != "-")
                            {
                                if (strEmsNoTemp == "-" || strEmsNoTemp == "" || strEmsNoTemp != strEmsNo)
                                {
                                    CmdP.CommandText = "UPDATE BusinessInfo SET EmsNo=@EmsNo WHERE BusinessAdminNo=@BusinessAdminNo";
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                                    CmdP.Parameters.AddWithValue("@BusinessAdminNo", strAdminNo);
                                    CmdP.ExecuteNonQuery();
                                }
                            }
                        }

                        #endregion

                        #region Step 2-4-4：名稱與地址搜尋 ComFacBizInfo。

                        if (strComFacBizName != string.Empty && strComFacBizName != "-" && Exist == false)
                        {
                            CmdP.CommandText = "SELECT TOP 1 AdminNo, EmsNo FROM ComFacBizInfo WHERE ComFacBizNameIndex=@ComFacBizNameIndex AND ComFacBizName=@ComFacBizName AND ComFacBizAddr=@ComFacBizAddr ORDER BY AdminNo";
                            CmdP.Parameters.Clear();
                            CmdP.Parameters.AddWithValue("@ComFacBizNameIndex", strComFacBizNameIndex);
                            CmdP.Parameters.AddWithValue("@ComFacBizName", strComFacBizName);
                            CmdP.Parameters.AddWithValue("@ComFacBizAddr", strComFacBizAddr);
                            SqlDataReader drComFacBizInfo = CmdP.ExecuteReader();

                            string strEmsNoTemp = string.Empty;

                            if (drComFacBizInfo.Read())
                            {
                                Exist = true;
                                strComFacBizType = "3";
                                strAdminNo = drComFacBizInfo["AdminNo"].ToString();
                                strEmsNoTemp = drComFacBizInfo["EmsNo"].ToString();
                            }
                            if (drComFacBizInfo != null)
                                drComFacBizInfo.Close();

                            if (Exist && strEmsNo != string.Empty && strEmsNo != "-")
                            {
                                if (strEmsNoTemp == "-" || strEmsNoTemp == "" || strEmsNoTemp != strEmsNo)
                                {
                                    CmdP.CommandText = "UPDATE ComFacBizInfo SET EmsNo=@EmsNo WHERE AdminNo=@AdminNo";
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                                    CmdP.Parameters.AddWithValue("@AdminNo", strAdminNo);
                                    CmdP.ExecuteNonQuery();
                                }
                            }
                        }

                        #endregion

                        #endregion

                        #region Step 2-5：若 Step 2-1 至 Step 2-4 都搜不到且廠商名稱為「-」，則用「環保署廠商基本資料表名稱」、「環保署廠商基本資料表地址」、「地址」搜尋 ComFacBizInfo。

                        #region Step 2-5-1：環保署廠商基本資料表名稱與環保署廠商基本資料表地址搜尋 ComFacBizInfo。

                        if (strComFacBizName == "-" && strEPACompanyName != string.Empty && strEPACompanyName != "-" && blEmsData && strFactoryAddress != "-" && Exist == false)
                        {
                            CmdP.CommandText = "SELECT AdminNo, ComFacBizAddr, EmsNo FROM ComFacBizInfo WHERE ComFacBizName=@ComFacBizName ORDER BY AdminNo";
                            CmdP.Parameters.Clear();
                            CmdP.Parameters.AddWithValue("@ComFacBizName", strEPACompanyName);
                            SqlDataAdapter daComFacBizInfo = new SqlDataAdapter(CmdP);
                            DataTable dtComFacBizInfo = new DataTable();
                            daComFacBizInfo.Fill(dtComFacBizInfo);
                            for (int i = 0; i < dtComFacBizInfo.Rows.Count; i++)
                            {
                                string strEmsNoTemp = string.Empty;

                                string strAddr = AddressAlignment(dtComFacBizInfo.Rows[i]["ComFacBizAddr"].ToString());
                                if (!string.IsNullOrEmpty(strAddr) && strFactoryAddress.Equals(strAddr))
                                {
                                    Exist = true;
                                    strComFacBizType = "3";
                                    strAdminNo = dtComFacBizInfo.Rows[i]["AdminNo"].ToString();
                                    strEmsNoTemp = dtComFacBizInfo.Rows[i]["EmsNo"].ToString();
                                }

                                if (Exist && strEmsNo != string.Empty && strEmsNo != "-")
                                {
                                    if (strEmsNoTemp == "-" || strEmsNoTemp == "" || strEmsNoTemp != strEmsNo)
                                    {
                                        CmdP.CommandText = "UPDATE ComFacBizInfo SET EmsNo=@EmsNo WHERE AdminNo=@AdminNo";
                                        CmdP.Parameters.Clear();
                                        CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                                        CmdP.Parameters.AddWithValue("@AdminNo", strAdminNo);
                                        CmdP.ExecuteNonQuery();
                                    }
                                }

                                if (Exist)
                                    break;
                            }
                        }

                        #endregion

                        #region Step 2-5-2：環保署廠商基本資料表名稱與地址搜尋 ComFacBizInfo。

                        if (strComFacBizName == "-" && strEPACompanyName != string.Empty && strEPACompanyName != "-" && Exist == false)
                        {
                            CmdP.CommandText = "SELECT TOP 1 AdminNo, EmsNo FROM ComFacBizInfo WHERE ComFacBizName=@ComFacBizName AND ComFacBizAddr=@ComFacBizAddr ORDER BY AdminNo";
                            CmdP.Parameters.Clear();
                            CmdP.Parameters.AddWithValue("@ComFacBizName", strEPACompanyName);
                            CmdP.Parameters.AddWithValue("@ComFacBizAddr", strComFacBizAddr);
                            SqlDataReader drComFacBizInfo = CmdP.ExecuteReader();

                            string strEmsNoTemp = string.Empty;

                            if (drComFacBizInfo.Read())
                            {
                                Exist = true;
                                strComFacBizType = "3";
                                strAdminNo = drComFacBizInfo["AdminNo"].ToString();
                                strEmsNoTemp = drComFacBizInfo["EmsNo"].ToString();
                            }
                            if (drComFacBizInfo != null)
                                drComFacBizInfo.Close();

                            if (Exist && strEmsNo != string.Empty && strEmsNo != "-")
                            {
                                if (strEmsNoTemp == "-" || strEmsNoTemp == "" || strEmsNoTemp != strEmsNo)
                                {
                                    CmdP.CommandText = "UPDATE ComFacBizInfo SET EmsNo=@EmsNo WHERE AdminNo=@AdminNo";
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                                    CmdP.Parameters.AddWithValue("@AdminNo", strAdminNo);
                                    CmdP.ExecuteNonQuery();
                                }
                            }
                        }

                        #endregion

                        #endregion
                    }

                    #endregion

                    #region Step 3：若來源資料不包含管制編號(EmsNo)，則進行一般比對邏輯。(與 Step 2 互斥)

                    else
                    {
                        #region Step 3-1：若有廠編則先用「廠編」搜尋 FactoryInfo。

                        if (strFactoryRegNo != string.Empty && strFactoryRegNo != "-" && Exist == false)
                        {
                            //以傳進來的 FactoryRegNo 去搜尋。
                            CmdP.CommandText = "SELECT TOP 1 FactoryRegNo, EmsNo FROM FactoryInfo WHERE FactoryRegNo = @FactoryRegNo";
                            CmdP.Parameters.Clear();
                            CmdP.Parameters.AddWithValue("@FactoryRegNo", strFactoryRegNo);
                            SqlDataReader drFactoryInfo = CmdP.ExecuteReader();

                            string strEmsNoTemp = string.Empty;

                            if (drFactoryInfo.Read())
                            {
                                Exist = true;
                                strComFacBizType = "1";
                                strAdminNo = strFactoryRegNo;
                                strEmsNoTemp = drFactoryInfo["EmsNo"].ToString();
                            }
                            if (drFactoryInfo != null)
                                drFactoryInfo.Close();

                            if (Exist && strEmsNo != string.Empty && strEmsNo != "-")
                            {
                                if (strEmsNoTemp == "-" || strEmsNoTemp == "")
                                {
                                    CmdP.CommandText = "UPDATE FactoryInfo SET EmsNo=@EmsNo WHERE FactoryRegNo=@FactoryRegNo";
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                                    CmdP.Parameters.AddWithValue("@FactoryRegNo", strFactoryRegNo);
                                    CmdP.ExecuteNonQuery();
                                }
                            }
                        }

                        #endregion

                        #region Step 3-2：若 Step 3-1 搜尋不到且有統編，則用「統編」搜尋 CompanyInfo。

                        if (strBusinessAdminNo != string.Empty && strBusinessAdminNo != "-" && Exist == false)
                        {
                            //以傳進來的 BusinessAdminNo 去搜尋。
                            CmdP.CommandText = "SELECT TOP 1 BusinessAdminNo FROM CompanyInfo WHERE BusinessAdminNo=@BusinessAdminNo";
                            CmdP.Parameters.Clear();
                            CmdP.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                            SqlDataReader drCompanyInfo = CmdP.ExecuteReader();
                            if (drCompanyInfo.Read())
                            {
                                Exist = true;
                                strComFacBizType = "0";
                                strAdminNo = strBusinessAdminNo;
                            }
                            if (drCompanyInfo != null)
                                drCompanyInfo.Close();
                        }

                        #endregion

                        #region Step 3-3：若 Step 3-1 及 Step 3-2 搜尋不到且有統編，則用「統編」搜尋 BusinessInfo。

                        if (strBusinessAdminNo != string.Empty && strBusinessAdminNo != "-" && Exist == false)
                        {
                            //以傳進來的 BusinessAdminNo 去搜尋。
                            CmdP.CommandText = "SELECT TOP 1 BusinessAdminNo FROM BusinessInfo WHERE BusinessAdminNo=@BusinessAdminNo";
                            CmdP.Parameters.Clear();
                            CmdP.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                            SqlDataReader drBusinessInfo = CmdP.ExecuteReader();
                            if (drBusinessInfo.Read())
                            {
                                Exist = true;
                                strComFacBizType = "2";
                                strAdminNo = strBusinessAdminNo;
                            }
                            if (drBusinessInfo != null)
                                drBusinessInfo.Close();
                        }

                        #endregion

                        #region Step 3-4：若 Step 3-1 至 Step 3-3 均不存在，則使用「名稱」進行絕對搜尋比對 FactoryInfo、CompanyInfo、BusinessInfo、ComFacBizInfo。

                        if (strComFacBizName != string.Empty && strComFacBizName != "-" && Exist == false)
                        {
                            result = funcSearchByName(strComFacBizName);
                            if (result != "")
                            {
                                Exist = true;   //若比對後 result 有值表示已存在。
                                strComFacBizType = result.Split(',')[0];
                                strAdminNo = result.Split(',')[1];
                            }
                        }

                        #endregion
                    }

                    #endregion

                    #region Step 4：上述比對後均不存在且廠商名稱為「-」，則需用廠編、統編及管編依序去搜尋其他的大表(ComFacBizInfo)。

                    if (Exist == false && strComFacBizName == "-" && (strEPACompanyName == string.Empty || strEPACompanyName == "-"))
                    {
                        if (strFactoryRegNo != "-")
                        {
                            //以傳進來的 FactoryRegNo 去搜尋其他表。
                            CmdP.CommandText = "SELECT TOP 1 AdminNo FROM ComFacBizInfo WHERE FactoryRegNo=@FactoryRegNo";
                            CmdP.Parameters.Clear();
                            CmdP.Parameters.AddWithValue("@FactoryRegNo", strFactoryRegNo);
                            SqlDataReader drFac = CmdP.ExecuteReader();
                            if (drFac.Read())
                            {
                                Exist = true;
                                strComFacBizType = "3";
                                strAdminNo = drFac["AdminNo"].ToString();
                            }
                            if (drFac != null)
                                drFac.Close();
                        }

                        if (strBusinessAdminNo != "-")
                        {
                            //以傳進來的 BusinessAdminNo 去搜尋其他表。
                            CmdP.CommandText = "SELECT TOP 1 AdminNo FROM ComFacBizInfo WHERE BusinessAdminNo=@BusinessAdminNo";
                            CmdP.Parameters.Clear();
                            CmdP.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                            SqlDataReader drCom = CmdP.ExecuteReader();
                            if (drCom.Read())
                            {
                                Exist = true;
                                strComFacBizType = "3";
                                strAdminNo = drCom["AdminNo"].ToString();
                            }
                            if (drCom != null)
                                drCom.Close();
                        }

                        if (strEmsNo != "-")
                        {
                            //以傳進來的 EmsNo 去搜尋其他表。
                            CmdP.CommandText = "SELECT TOP 1 AdminNo FROM ComFacBizInfo WHERE EmsNo=@EmsNo";
                            CmdP.Parameters.Clear();
                            CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                            SqlDataReader drEms = CmdP.ExecuteReader();
                            if (drEms.Read())
                            {
                                Exist = true;
                                strComFacBizType = "3";
                                strAdminNo = drEms["AdminNo"].ToString();
                            }
                            if (drEms != null)
                                drEms.Close();
                        }
                    }

                    //若來源資料廠商名稱、統編、管編、廠編皆為「-」，則回傳空白，表示來源資料錯誤。
                    if (strComFacBizName == "-" && strBusinessAdminNo == "-" && strEmsNo == "-" && strFactoryRegNo == "-")
                    {
                        Exist = true;
                        strComFacBizType = "";
                        strAdminNo = "";
                    }

                    #endregion

                    #region Step 5：上述比對後均不存在，則將廠商資料新增至其他大表(ComFacBizInfo)。
                    
                    if (Exist == false)
                    {
                        string strComFacBizNameFinal = strComFacBizName;
                        if (strComFacBizName == "-" && (strEPACompanyName != string.Empty && strEPACompanyName != "-"))
                            strComFacBizNameFinal = strEPACompanyName;

                        string strComFacBizAddrFinal = strComFacBizAddr;
                        if (strFactoryAddressOrig != string.Empty && strFactoryAddressOrig != "-")
                            strComFacBizAddrFinal = strFactoryAddressOrig;

                        result = InsNoExistComBizFacInfo(strEmsNo, strBusinessAdminNo, strFactoryRegNo, strComFacBizNameFinal, strComFacBizAddrFinal, strTransId, strSourseTableName, strNo);
                    }
                    else
                        result = strComFacBizType + "," + strAdminNo;

                    #endregion

                    ConnP.Dispose();
                    if (ConnP != null)
                        ConnP.Close();
                    CmdP.Dispose();
                }
                catch (Exception ex)
                {
                    //若發生錯誤則需要將錯誤訊息新增至 DataTransErrorLog。
                    funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strSourseTableName, ex.Message, "ComFacBizMatch.MainFunc()");
                    Console.WriteLine("ComFacBizMatch.MainFunc()錯誤，" + ex.Message);
                }

                #endregion
            }
            else
            {
                switch (strSourseTableName)
                {
                    case "TFtyDecChemiFlow":
                        //將 strBusinessAdminNo、strFactoryRegNo、strComFacBizName、strComFacBizAddr 四個欄位做 Primary Key 處理，
                        //且只針對第四張其他大表(ComFacBizInfo)做搜尋及更新。

                        #region 傳入參數去除空白字元與補上 Primary Key 為空值須修正為「-」

                        if (string.IsNullOrEmpty(strBusinessAdminNo) || string.IsNullOrWhiteSpace(strBusinessAdminNo))
                            strBusinessAdminNo = "-";
                        else
                            strBusinessAdminNo = strBusinessAdminNo.Trim();

                        if (string.IsNullOrEmpty(strFactoryRegNo) || string.IsNullOrWhiteSpace(strFactoryRegNo))
                            strFactoryRegNo = "-";
                        else
                            strFactoryRegNo = strFactoryRegNo.Trim();

                        if (string.IsNullOrEmpty(strComFacBizName) || string.IsNullOrWhiteSpace(strComFacBizName))
                            strComFacBizName = "-";
                        else
                            strComFacBizName = strComFacBizName.Trim();

                        if (string.IsNullOrEmpty(strComFacBizAddr))
                        {
                            strComFacBizAddr = "-";
                            strComFacBizAddrPK = "-";
                        }
                        else
                        {
                            strComFacBizAddr = strComFacBizAddr.Trim();
                            if (strComFacBizAddr.Trim().Length > 250)
                                strComFacBizAddrPK = strComFacBizAddr.Trim().Substring(strComFacBizAddr.Trim().Length - 250);
                            else
                                strComFacBizAddrPK = strComFacBizAddr.Trim();
                        }

                        #endregion

                        #region 先搜尋 SourceComData 是否存在該筆資料，存在則直接回傳該筆 ComFacBizType 及 AdminNo。
                        
                        try
                        {
                            SqlConnection Conn = new SqlConnection(strPrimaryDBConn);
                            SqlCommand Cmd = Conn.CreateCommand();
                            Conn.Open();
                            Cmd.CommandTimeout = 3000;

                            Cmd.CommandText = "SELECT ComFacBizType, AdminNo, EmsNo, BusinessAdminNo, FactoryRegNo, SourceComName, CompanyAddress, TempTableName FROM SourceComData" + strDepId + 
                                            " WHERE EmsNo=@EmsNo AND BusinessAdminNo=@BusinessAdminNo AND FactoryRegNo=@FactoryRegNo AND SourceComName=@SourceComName AND CompanyAddress=@CompanyAddress AND TempTableName=@TempTableName";
                            Cmd.Parameters.Clear();
                            Cmd.Parameters.AddWithValue("@EmsNo", "-");
                            Cmd.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                            Cmd.Parameters.AddWithValue("@FactoryRegNo", strFactoryRegNo);
                            Cmd.Parameters.AddWithValue("@SourceComName", strComFacBizName);
                            Cmd.Parameters.AddWithValue("@CompanyAddress", strComFacBizAddrPK);
                            Cmd.Parameters.AddWithValue("@TempTableName", strSourseTableName);
                            SqlDataReader drSource = Cmd.ExecuteReader();
                            if (drSource.Read())
                            {
                                SourceExist = true;
                                strComFacBizType = drSource["ComFacBizType"].ToString();
                                strAdminNo = drSource["AdminNo"].ToString();
                            }
                            if (drSource != null)
                                drSource.Close();
                            if (SourceExist)
                                result = strComFacBizType + "," + strAdminNo;

                            Conn.Dispose();
                            if (Conn != null)
                                Conn.Close();
                            Cmd.Dispose();
                        }
                        catch (Exception ex)
                        {
                            //若發生錯誤則需要將錯誤訊息新增至 DataTransErrorLog。
                            funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strSourseTableName, ex.Message, "ComFacBizMatch.SourceComData.Custom");
                            Console.WriteLine("ComFacBizMatch.SourceComData.Custom錯誤，" + ex.Message);
                        }

                        if (SourceExist)
                            return result;

                        #endregion

                        #region 客製化需求，經濟部 工廠申報選定化學物質流向資料(TFtyDecChemiFlow)下游公司
                        
                        try
                        {
                            //因為需要從 Primary DB 取出轉置的資料，需要定義 Primary DB 連線。
                            SqlConnection ConnP = new SqlConnection(strPrimaryDBConn);
                            SqlCommand CmdP = ConnP.CreateCommand();
                            ConnP.Open();
                            CmdP.CommandTimeout = 3000;

                            #region 先使用公司、工廠及商登名稱進行絕對搜尋比對(CompanyInfo、BusinessInfo、FactoryInfo 及 ComFacBizInfo)
                            if (Exist == false && strComFacBizName != "-")
                                result = funcSearchByName(strComFacBizName);
                            if (result != "")
                                Exist = true;   //若比對後 result 有值表示已存在。

                            if (Exist == false && strComFacBizName == "-")
                            {
                                if (strFactoryRegNo != "-")
                                {
                                    //以傳進來的 FactoryRegNo 去搜尋其他表。
                                    CmdP.CommandText = "SELECT TOP 1 AdminNo FROM ComFacBizInfo WHERE FactoryRegNo=@FactoryRegNo";
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@FactoryRegNo", strFactoryRegNo);
                                    SqlDataReader drFac = CmdP.ExecuteReader();
                                    if (drFac.Read())
                                    {
                                        Exist = true;
                                        strComFacBizType = "3";
                                        strAdminNo = drFac["AdminNo"].ToString();
                                    }
                                    if (drFac != null)
                                        drFac.Close();
                                }

                                if (strBusinessAdminNo != "-")
                                {
                                    //以傳進來的 BusinessAdminNo 去搜尋其他表。
                                    CmdP.CommandText = "SELECT TOP 1 AdminNo FROM ComFacBizInfo WHERE BusinessAdminNo=@BusinessAdminNo";
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                                    SqlDataReader drCom = CmdP.ExecuteReader();
                                    if (drCom.Read())
                                    {
                                        Exist = true;
                                        strComFacBizType = "3";
                                        strAdminNo = drCom["AdminNo"].ToString();
                                    }
                                    if (drCom != null)
                                        drCom.Close();
                                }

                                if (strEmsNo != "-")
                                {
                                    //以傳進來的 EmsNo 去搜尋其他表。
                                    CmdP.CommandText = "SELECT TOP 1 AdminNo FROM ComFacBizInfo WHERE EmsNo=@EmsNo";
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                                    SqlDataReader drEms = CmdP.ExecuteReader();
                                    if (drEms.Read())
                                    {
                                        Exist = true;
                                        strComFacBizType = "3";
                                        strAdminNo = drEms["AdminNo"].ToString();
                                    }
                                    if (drEms != null)
                                        drEms.Close();
                                }
                            }

                            #endregion
                            
                            #region Exist in ComFacBizInfo or not

                            //若全部都為-，則不塞進其他表，直接回傳空。
                            if (strComFacBizName == "-" && strAdminNo == "-" && strEmsNo == "-" && strFactoryRegNo == "-")
                            {
                                Exist = true;
                                result = ",";
                            }
                            
                            if (Exist == false)
                                result = InsNoExistComBizFacInfoCustomized(strBusinessAdminNo, strFactoryRegNo, strComFacBizName, strComFacBizAddr, strTransId, strSourseTableName, strNo);                               
                            #endregion

                            ConnP.Dispose();
                            if (ConnP != null)
                                ConnP.Close();
                            CmdP.Dispose();
                        }
                        catch (Exception ex)
                        {
                            //若發生錯誤則需要將錯誤訊息新增至 DataTransErrorLog。
                            funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strSourseTableName, ex.Message, "ComFacBizMatch.MainFunc(strEmsNo==strSourseTableName)");
                            Console.WriteLine("ComFacBizMatch.MainFunc(strEmsNo==strSourseTableName)錯誤，" + ex.Message);
                        }

                        #endregion
                        
                        break;
                    case "TCommonDangerItemTYC":
                    case "TCommonDangerItemNTPC":
                    case "TCommonDangerItemTPI":
                    case "TCommonDangerItemTXG":
                        //將 strBusinessAdminNo、strFactoryRegNo、strComFacBizName、strComFacBizAddr 四個欄位做 Primary Key 處理，
                        //且只針對第四張其他大表(ComFacBizInfo)做搜尋及更新。
                    
                        #region 傳入參數去除空白字元與補上 Primary Key 為空值須修正為「-」

                        if (string.IsNullOrEmpty(strBusinessAdminNo) || string.IsNullOrWhiteSpace(strBusinessAdminNo))
                            strBusinessAdminNo = "-";
                        else
                            strBusinessAdminNo = strBusinessAdminNo.Trim();

                        if (string.IsNullOrEmpty(strFactoryRegNo) || string.IsNullOrWhiteSpace(strFactoryRegNo))
                            strFactoryRegNo = "-";
                        else
                            strFactoryRegNo = strFactoryRegNo.Trim();

                        if (string.IsNullOrEmpty(strComFacBizName) || string.IsNullOrWhiteSpace(strComFacBizName))
                            strComFacBizName = "-";
                        else
                            strComFacBizName = strComFacBizName.Trim();

                        if (string.IsNullOrEmpty(strComFacBizAddr))
                        {
                            strComFacBizAddr = "-";
                            strComFacBizAddrPK = "-";
                        }
                        else
                        {
                            strComFacBizAddr = strComFacBizAddr.Trim();
                            if (strComFacBizAddr.Trim().Length > 250)
                                strComFacBizAddrPK = strComFacBizAddr.Trim().Substring(strComFacBizAddr.Trim().Length - 250);
                            else
                                strComFacBizAddrPK = strComFacBizAddr.Trim();
                        }
                        #endregion

                        #region 先搜尋 SourceComData 是否存在該筆資料，存在則直接回傳該筆 ComFacBizType 及 AdminNo。
                        
                        try
                        {
                            SqlConnection Conn = new SqlConnection(strPrimaryDBConn);
                            SqlCommand Cmd = Conn.CreateCommand();
                            Conn.Open();
                            Cmd.CommandTimeout = 3000;

                            Cmd.CommandText = "SELECT ComFacBizType, AdminNo, EmsNo, BusinessAdminNo, FactoryRegNo, SourceComName, CompanyAddress, TempTableName FROM SourceComData" + strDepId +
                                            " WHERE EmsNo=@EmsNo AND BusinessAdminNo=@BusinessAdminNo AND FactoryRegNo=@FactoryRegNo AND SourceComName=@SourceComName AND CompanyAddress=@CompanyAddress AND TempTableName=@TempTableName";
                            Cmd.Parameters.Clear();
                            Cmd.Parameters.AddWithValue("@EmsNo", "-");
                            Cmd.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                            Cmd.Parameters.AddWithValue("@FactoryRegNo", strFactoryRegNo);
                            Cmd.Parameters.AddWithValue("@SourceComName", strComFacBizName);
                            Cmd.Parameters.AddWithValue("@CompanyAddress", strComFacBizAddrPK);
                            Cmd.Parameters.AddWithValue("@TempTableName", strSourseTableName);
                            SqlDataReader drSource = Cmd.ExecuteReader();
                            if (drSource.Read())
                            {
                                SourceExist = true;
                                strComFacBizType = drSource["ComFacBizType"].ToString();
                                strAdminNo = drSource["AdminNo"].ToString();
                            }
                            if (drSource != null)
                                drSource.Close();
                            if (SourceExist)
                                result = strComFacBizType + "," + strAdminNo;
                                

                            Conn.Dispose();
                            if (Conn != null)
                                Conn.Close();
                            Cmd.Dispose();

                        }
                        catch (Exception ex)
                        {
                            //若發生錯誤則需要將錯誤訊息新增至 DataTransErrorLog。
                            funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strSourseTableName, ex.Message, "ComFacBizMatch.SourceComData.Custom");
                            Console.WriteLine("ComFacBizMatch.SourceComData.Custom錯誤，" + ex.Message);
                        }

                        if (SourceExist)
                            return result;
                        
                        #endregion

                        #region 客製化需求，消防局消防安全檢查列管系統資料(TCommonDangerItem)申報公司

                        try
                        {
                            //因為需要從 Primary DB 取出轉置的資料，需要定義 Primary DB 連線。
                            SqlConnection ConnP = new SqlConnection(strPrimaryDBConn);
                            SqlCommand CmdP = ConnP.CreateCommand();
                            ConnP.Open();
                            CmdP.CommandTimeout = 3000;

                            #region 先使用公司、工廠及商登名稱進行絕對搜尋比對(CompanyInfo、BusinessInfo、FactoryInfo 及 ComFacBizInfo)
                            if (Exist == false && strComFacBizName != "-")
                                //result = funcSearchByName(strComFacBizName);
                                result = funcSearchForFD(strComFacBizName, strComFacBizAddr, strBusinessAdminNo,strFactoryRegNo);
                               
                            if (result != "")
                                Exist = true;   //若比對後 result 有值表示已存在。

                            if (Exist == false && strComFacBizName == "-")
                            {
                                if (strFactoryRegNo != "-")
                                {
                                    //以傳進來的 FactoryRegNo 去搜尋其他表。
                                    CmdP.CommandText = "SELECT TOP 1 AdminNo FROM ComFacBizInfo WHERE FactoryRegNo=@FactoryRegNo";
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@FactoryRegNo", strFactoryRegNo);
                                    SqlDataReader drFac = CmdP.ExecuteReader();
                                    if (drFac.Read())
                                    {
                                        Exist = true;
                                        strComFacBizType = "3";
                                        strAdminNo = drFac["AdminNo"].ToString();
                                    }
                                    if (drFac != null)
                                        drFac.Close();
                                }

                                if (strBusinessAdminNo != "-")
                                {
                                    //以傳進來的 BusinessAdminNo 去搜尋其他表。
                                    CmdP.CommandText = "SELECT TOP 1 AdminNo FROM ComFacBizInfo WHERE BusinessAdminNo=@BusinessAdminNo";
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                                    SqlDataReader drCom = CmdP.ExecuteReader();
                                    if (drCom.Read())
                                    {
                                        Exist = true;
                                        strComFacBizType = "3";
                                        strAdminNo = drCom["AdminNo"].ToString();
                                    }
                                    if (drCom != null)
                                        drCom.Close();
                                }

                                if (strEmsNo != "-")
                                {
                                    //以傳進來的 EmsNo 去搜尋其他表。
                                    CmdP.CommandText = "SELECT TOP 1 AdminNo FROM ComFacBizInfo WHERE EmsNo=@EmsNo";
                                    CmdP.Parameters.Clear();
                                    CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                                    SqlDataReader drEms = CmdP.ExecuteReader();
                                    if (drEms.Read())
                                    {
                                        Exist = true;
                                        strComFacBizType = "3";
                                        strAdminNo = drEms["AdminNo"].ToString();
                                    }
                                    if (drEms != null)
                                        drEms.Close();
                                }
                            }

                            #endregion

                            #region Exist in ComFacBizInfo or not

                            //若全部都為-，則不塞進其他表，直接回傳空。
                            if (strComFacBizName == "-" && strAdminNo == "-" && strEmsNo == "-" && strFactoryRegNo == "-")
                            {
                                Exist = true;
                                result = ",";
                            }

                            if (Exist == false)
                                result = InsNoExistComBizFacInfo("-", strBusinessAdminNo, strFactoryRegNo, strComFacBizName, strComFacBizAddr, strTransId, strSourseTableName, strNo);
                            #endregion

                            ConnP.Dispose();
                            if (ConnP != null)
                                ConnP.Close();
                            CmdP.Dispose();
                        }
                        catch (Exception ex)
                        {
                            //若發生錯誤則需要將錯誤訊息新增至 DataTransErrorLog。
                            funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strSourseTableName, ex.Message, "ComFacBizMatch.MainFunc(strEmsNo==strSourseTableName)");
                            Console.WriteLine("ComFacBizMatch.MainFunc(strEmsNo==strSourseTableName)錯誤，" + ex.Message);
                        }

                        #endregion

                        break;
                    default:
                        break;
                }
            }

            #region Step Final：SourceComData 不存在傳入的資料，需新增該筆至本表格中。
            if (strEmsNo != strSourseTableName)
            {
                #region 正常處理流程
                if (!(result == string.Empty || result == "," || result == "-,-" || result == ",-" || result == "-,"))
                {
                    //因為 result == "," || result == "-,-" || result == ",-" || result == "-," 代表資料有誤，就不新增。
                    if (!SourceExist)
                    {
                        try
                        {
                            //不存在需要將傳入的資料新增至 SourceComData 表格中。
                            SqlConnection Conn = new SqlConnection(strPrimaryDBConn);
                            SqlCommand Cmd = Conn.CreateCommand();
                            Conn.Open();
                            Cmd.CommandTimeout = 3000;
                            Cmd.CommandText = "INSERT INTO SourceComData" + strDepId
                                + "(ComFacBizType, AdminNo, EmsNo, BusinessAdminNo, FactoryRegNo, SourceComName, CompanyAddress, TempTableName, CompanyFullAddr, MappingSN)"
                                + " VALUES(@ComFacBizType, @AdminNo, @EmsNo, @BusinessAdminNo, @FactoryRegNo, @SourceComName, @CompanyAddress, @TempTableName, @CompanyFullAddr, @MappingSN)";
                            Cmd.Parameters.Clear();
                            Cmd.Parameters.AddWithValue("@ComFacBizType", result.Split(',')[0]);
                            Cmd.Parameters.AddWithValue("@AdminNo", result.Split(',')[1]);
                            Cmd.Parameters.AddWithValue("@EmsNo", strEmsNo);
                            Cmd.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                            Cmd.Parameters.AddWithValue("@FactoryRegNo", strFactoryRegNo);
                            Cmd.Parameters.AddWithValue("@SourceComName", strComFacBizName);
                            Cmd.Parameters.AddWithValue("@CompanyAddress", strComFacBizAddrPK);
                            Cmd.Parameters.AddWithValue("@TempTableName", strSourseTableName);
                            Cmd.Parameters.AddWithValue("@CompanyFullAddr", strComFacBizAddr);
                            Cmd.Parameters.AddWithValue("@MappingSN", strTransId + strNo);
                            Cmd.ExecuteNonQuery();

                            Conn.Dispose();
                            if (Conn != null)
                                Conn.Close();
                            Cmd.Dispose();
                        }
                        catch (Exception ex)
                        {
                            //若發生錯誤則需要將錯誤訊息新增至 DataTransErrorLog。
                            funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strSourseTableName, ex.Message, "ComFacBizMatch.SourceComData(INS)");
                            Console.WriteLine("ComFacBizMatch.SourceComData(INS)錯誤，" + ex.Message);
                        }
                    }
                }
                #endregion
            }
            else
            {
                #region 客製化處理流程
                //客製化處理
                switch (strSourseTableName)
                {
                    case "TCommonDangerItemTYC":
                    case "TCommonDangerItemNTPC":
                    case "TCommonDangerItemTPI":
                    case "TCommonDangerItemTXG":
                    case "TFtyDecChemiFlow":
                        if (!(result == string.Empty || result == "," || result == "-,-" || result == ",-" || result == "-,"))
                        {
                            //因為 result == "," || result == "-,-" || result == ",-" || result == "-," 代表資料有誤，就不新增。
                            if (!SourceExist)
                            {
                                try
                                {
                                    //不存在需要將傳入的資料新增至 SourceComData 表格中。
                                    SqlConnection Conn = new SqlConnection(strPrimaryDBConn);
                                    SqlCommand Cmd = Conn.CreateCommand();
                                    Conn.Open();
                                    Cmd.CommandTimeout = 3000;
                                    Cmd.CommandText = "INSERT INTO SourceComData" + strDepId
                                        + "(ComFacBizType, AdminNo, EmsNo, BusinessAdminNo, FactoryRegNo, SourceComName, CompanyAddress, TempTableName, CompanyFullAddr, MappingSN)"
                                        + " VALUES(@ComFacBizType, @AdminNo, @EmsNo, @BusinessAdminNo, @FactoryRegNo, @SourceComName, @CompanyAddress, @TempTableName, @CompanyFullAddr, @MappingSN)";
                                    Cmd.Parameters.Clear();
                                    Cmd.Parameters.AddWithValue("@ComFacBizType", result.Split(',')[0]);
                                    Cmd.Parameters.AddWithValue("@AdminNo", result.Split(',')[1]);
                                    Cmd.Parameters.AddWithValue("@EmsNo", "-");
                                    Cmd.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                                    Cmd.Parameters.AddWithValue("@FactoryRegNo", strFactoryRegNo);
                                    Cmd.Parameters.AddWithValue("@SourceComName", strComFacBizName);
                                    Cmd.Parameters.AddWithValue("@CompanyAddress", strComFacBizAddrPK);
                                    Cmd.Parameters.AddWithValue("@TempTableName", strSourseTableName);
                                    Cmd.Parameters.AddWithValue("@CompanyFullAddr", strComFacBizAddr);
                                    Cmd.Parameters.AddWithValue("@MappingSN", strTransId + strNo);
                                    Cmd.ExecuteNonQuery();

                                    Conn.Dispose();
                                    if (Conn != null)
                                        Conn.Close();
                                    Cmd.Dispose();
                                }
                                catch (Exception ex)
                                {
                                    //若發生錯誤則需要將錯誤訊息新增至 DataTransErrorLog。
                                    funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strSourseTableName, ex.Message, "ComFacBizMatch.SourceComData.Custom(INS)");
                                    Console.WriteLine("ComFacBizMatch.SourceComData.Custom(INS)錯誤，" + ex.Message);
                                }
                            }
                        }
                        break;
                    default:
                        break;
                }
                #endregion
            }
            #endregion

            return result;
        }

        /// <summary>
        /// 若透過 ComFacBizMatch.MainFunc 取不到任何對應的公司、工廠及營利事業資訊，則需透過此 Func 去新增資料。
        /// </summary>
        /// <param name="strEmsNo">管制編號</param>
        /// <param name="strBusinessAdminNo">公司統一編號</param>
        /// <param name="strFactoryRegNo">工廠登記證號</param>
        /// <param name="strComFacBizName">公司/工廠/營利事業名稱</param>
        /// <param name="strComFacBizAddr">公司/工廠/營利事業地址</param>
        /// <param name="strTransId">來源端的交易編號</param>
        /// <param name="strSourseTableName">來源端的資料表名稱</param>
        /// <param name="strNo">來源端的XML的筆數</param>
        /// <returns>
        /// 回傳 ComFacBizType,AdminNo 組合字串。
        /// ComFacBizType：0-公司；1-工廠；2-營利事業；3-其他
        /// AdminNo：0-公司統一編號/1-工廠登記證號/2-營利事業統一編號/3-唯一值，流水號遞增
        /// </returns>
        private static string InsNoExistComBizFacInfo(string strEmsNo, string strBusinessAdminNo, string strFactoryRegNo,
                                                      string strComFacBizName, string strComFacBizAddr,
                                                      string strTransId, string strSourseTableName, string strNo)
        {
            string strComFacBizType = string.Empty;
            string strAdminNo = string.Empty;

            try
            {
                //因為需要從 Primary DB 取出轉置的資料，需要定義 Primary DB 連線。
                SqlConnection ConnP = new SqlConnection(strPrimaryDBConn);
                SqlCommand CmdP = ConnP.CreateCommand();
                ConnP.Open();
                CmdP.CommandTimeout = 3000;

                #region Step 1：新增資料至 ComBizFacInfo 表格
                string strComFacBizNameIndex = "";
                if (strComFacBizName.Length > 1)
                    strComFacBizNameIndex = strComFacBizName.Substring(0, 1);
                else
                    strComFacBizNameIndex = strComFacBizName;

                CmdP.CommandText = "INSERT INTO ComFacBizInfo" +
                                    " (BusinessAdminNo, EmsNo, FactoryRegNo, ComFacBizName, ComFacBizAddr," +
                                    " TransId, PrimaryTableName, IsConverted, No, ComFacBizNameIndex)" +
                                    " VALUES" +
                                    " (@BusinessAdminNo, @EmsNo, @FactoryRegNo, @ComFacBizName, @ComFacBizAddr," +
                                    " @TransId, @PrimaryTableName, '0', @No, @ComFacBizNameIndex)";
                CmdP.Parameters.Clear();
                CmdP.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                CmdP.Parameters.AddWithValue("@FactoryRegNo", strFactoryRegNo);
                CmdP.Parameters.AddWithValue("@ComFacBizName", strComFacBizName);
                CmdP.Parameters.AddWithValue("@ComFacBizAddr", strComFacBizAddr);
                CmdP.Parameters.AddWithValue("@TransId", strTransId);
                CmdP.Parameters.AddWithValue("@PrimaryTableName", strSourseTableName);
                CmdP.Parameters.AddWithValue("@No", strNo);
                CmdP.Parameters.AddWithValue("@ComFacBizNameIndex", strComFacBizNameIndex);
                CmdP.ExecuteNonQuery();
                #endregion

                #region Step 2：再從 ComBizFacInfo 表格取出剛剛新增的資料
                CmdP.CommandText = "SELECT TOP 1 AdminNo FROM ComFacBizInfo WHERE" +
                                    " EmsNo = @EmsNo AND" +
                                    " BusinessAdminNo = @BusinessAdminNo AND" +
                                    " FactoryRegNo = @FactoryRegNo AND" +
                                    " ComFacBizName = @ComFacBizName AND" +
                                    " ComFacBizAddr = @ComFacBizAddr";
                CmdP.Parameters.Clear();
                CmdP.Parameters.AddWithValue("@EmsNo", strEmsNo);
                CmdP.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                CmdP.Parameters.AddWithValue("@FactoryRegNo", strFactoryRegNo);
                CmdP.Parameters.AddWithValue("@ComFacBizName", strComFacBizName);
                CmdP.Parameters.AddWithValue("@ComFacBizAddr", strComFacBizAddr);
                SqlDataReader drComFacBizInfo = CmdP.ExecuteReader();
                if (drComFacBizInfo.Read())
                {
                    strComFacBizType = "3";
                    strAdminNo = drComFacBizInfo["AdminNo"].ToString();
                }
                if (drComFacBizInfo != null)
                    drComFacBizInfo.Close();
                #endregion

                ConnP.Dispose();
                if (ConnP != null)
                    ConnP.Close();
                CmdP.Dispose();
            }
            catch (Exception ex)
            {
                //若發生錯誤則需要將錯誤訊息新增至 DataTransErrorLog。
                funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strSourseTableName, ex.Message,
                                                 "ComFacBizMatch.InsNoExistComBizFacInfo()");
                Console.WriteLine("ComFacBizMatch.InsNoExistComBizFacInfo()錯誤，" + ex.Message);
            }

            return (strComFacBizType + "," + strAdminNo);
        }

        /// <summary>
        /// (客製化)若透過 ComFacBizMatch.MainFunc 取不到任何對應的公司、工廠及營利事業資訊，則需透過此 Func 去新增資料。
        /// </summary>
        /// <param name="strBusinessAdminNo">公司統一編號</param>
        /// <param name="strFactoryRegNo">工廠登記證號</param>
        /// <param name="strComFacBizName">公司/工廠/營利事業名稱</param>
        /// <param name="strComFacBizAddr">公司/工廠/營利事業地址</param>
        /// <param name="strTransId">來源端的交易編號</param>
        /// <param name="strSourseTableName">來源端的資料表名稱</param>
        /// <param name="strNo">來源端的XML的筆數</param>
        /// <returns>
        /// 回傳 ComFacBizType,AdminNo 組合字串。
        /// ComFacBizType：0-公司；1-工廠；2-營利事業；3-其他
        /// AdminNo：0-公司統一編號/1-工廠登記證號/2-營利事業統一編號/3-唯一值，流水號遞增
        /// </returns>
        private static string InsNoExistComBizFacInfoCustomized(string strBusinessAdminNo, string strFactoryRegNo,
                                                                string strComFacBizName, string strComFacBizAddr,
                                                                string strTransId, string strSourseTableName, string strNo)
        {
            string strComFacBizType = string.Empty;
            string strAdminNo = string.Empty;

            try
            {
                //因為需要從 Primary DB 取出轉置的資料，需要定義 Primary DB 連線。
                SqlConnection ConnP = new SqlConnection(strPrimaryDBConn);
                SqlCommand CmdP = ConnP.CreateCommand();
                ConnP.Open();
                CmdP.CommandTimeout = 3000;

                #region Step 1：新增資料至 ComBizFacInfo 表格
                string strComFacBizNameIndex = "";
                if (strComFacBizName.Length > 1)
                    strComFacBizNameIndex = strComFacBizName.Substring(0, 1);
                else
                    strComFacBizNameIndex = strComFacBizName;

                CmdP.CommandText = "INSERT INTO ComFacBizInfo" +
                                    " (BusinessAdminNo, EmsNo, FactoryRegNo, ComFacBizName, ComFacBizAddr," +
                                    " TransId, PrimaryTableName, IsConverted, No, ComFacBizNameIndex)" +
                                    " VALUES" +
                                    " (@BusinessAdminNo, '-', @FactoryRegNo, @ComFacBizName, @ComFacBizAddr," +
                                    " @TransId, @PrimaryTableName, '0', @No, @ComFacBizNameIndex)";
                CmdP.Parameters.Clear();
                CmdP.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                CmdP.Parameters.AddWithValue("@FactoryRegNo", strFactoryRegNo);
                CmdP.Parameters.AddWithValue("@ComFacBizName", strComFacBizName);
                CmdP.Parameters.AddWithValue("@ComFacBizAddr", strComFacBizAddr);
                CmdP.Parameters.AddWithValue("@TransId", strTransId);
                CmdP.Parameters.AddWithValue("@PrimaryTableName", strSourseTableName);
                CmdP.Parameters.AddWithValue("@No", strNo);
                CmdP.Parameters.AddWithValue("@ComFacBizNameIndex", strComFacBizNameIndex);
                CmdP.ExecuteNonQuery();
                #endregion

                #region Step 2：再從 ComBizFacInfo 表格取出剛剛新增的資料
                CmdP.CommandText = "SELECT TOP 1 AdminNo FROM ComFacBizInfo WHERE" +
                                    " BusinessAdminNo = @BusinessAdminNo AND" +
                                    " FactoryRegNo = @FactoryRegNo AND" +
                                    " ComFacBizName = @ComFacBizName AND" +
                                    " ComFacBizAddr = @ComFacBizAddr";
                CmdP.Parameters.Clear();
                CmdP.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                CmdP.Parameters.AddWithValue("@FactoryRegNo", strFactoryRegNo);
                CmdP.Parameters.AddWithValue("@ComFacBizName", strComFacBizName);
                CmdP.Parameters.AddWithValue("@ComFacBizAddr", strComFacBizAddr);
                SqlDataReader drComFacBizInfo = CmdP.ExecuteReader();
                if (drComFacBizInfo.Read())
                {
                    strComFacBizType = "3";
                    strAdminNo = drComFacBizInfo["AdminNo"].ToString();
                }
                if (drComFacBizInfo != null)
                    drComFacBizInfo.Close();
                #endregion

                ConnP.Dispose();
                if (ConnP != null)
                    ConnP.Close();
                CmdP.Dispose();
            }
            catch (Exception ex)
            {
                //若發生錯誤則需要將錯誤訊息新增至 DataTransErrorLog。
                funcUtil.ins1stDataTransErrorLog(strNo, strTransId, strSourseTableName, ex.Message,
                                                 "ComFacBizMatch.InsNoExistComBizFacInfoCustomized()");
                Console.WriteLine("ComFacBizMatch.InsNoExistComBizFacInfo()錯誤，" + ex.Message);
            }

            return (strComFacBizType + "," + strAdminNo);
        }

        /// <summary>
        /// 科學記號轉換小數數值
        /// </summary>
        /// <param name="SNno">傳入含有科學記號的數值</param>
        /// <returns>經過轉換後的數值</returns>
        public static String ScientificNotation(String SNno)
        {
            String result = "";
            try
            {
                SNno = SNno.Replace("e","E");
                String[] cut = SNno.Split('E');
                Decimal num = Decimal.Parse(cut[0]);
                String type = cut[1].Substring(0,1);
                Double exponenttemp = Double.Parse(cut[1].Substring(1, (cut[1].Length - 1)));
                Decimal exponent = 0;
                Decimal answer = 0;
                exponent = (Decimal)Math.Pow(10, exponenttemp);
                if(type.Equals("-"))
                {
                    exponent = Decimal.Divide(1, exponent);
                }
                answer = Decimal.Multiply(num, exponent);
                result = answer.ToString();
            }
            catch
            {
                result = "This is not Scientific Notation";
            }
            return result;
        }

        /// <summary>
        /// 直接以公司/工廠/營利事業名稱搜尋四張大表，不以 PK 做搜尋。
        /// </summary>
        /// <param name="strComFacBizName">公司/工廠/營利事業名稱</param>
        /// <returns>
        /// 回傳 ComFacBizType,AdminNo 組合字串。
        /// ComFacBizType：0-公司；1-工廠；2-營利事業；3-其他
        /// AdminNo：0-公司統一編號/1-工廠登記證號/2-營利事業統一編號/3-唯一值，流水號遞增
        /// </returns>
        public static string funcSearchByName(string strComFacBizName)
        {
            var strComFacBizType = string.Empty;    //暫存 ComFacBizType (0-公司；1-工廠；2-營利事業；3-其他)
            var strAdminNo = string.Empty;          //暫存 AdminNo (0-公司統一編號 / 1-工廠登記證號/ 2-營利事業統一編號/ 3-唯一值，識別用：遞增流水號)
            bool Exist = false;                     //暫存判斷資料搜尋後是否存在

            #region 傳入公司/工廠/營利事業名稱若為「-」或是空白，則回傳無。
            if (strComFacBizName == "-" || string.IsNullOrEmpty(strComFacBizName) || string.IsNullOrWhiteSpace(strComFacBizName))
                return "";

            string strComFacBizNameIndex = "";
            if (strComFacBizName.Length > 1)
                strComFacBizNameIndex = strComFacBizName.Substring(0, 1);
            else
                strComFacBizNameIndex = strComFacBizName;
            #endregion

            try
            {
                //因為需要從 Primary DB 取出轉置的資料，需要定義 Primary DB 連線。
                SqlConnection ConnP = new SqlConnection(strPrimaryDBConn);
                SqlCommand CmdP = ConnP.CreateCommand();
                ConnP.Open();
                CmdP.CommandTimeout = 3000;

                #region Exist in FactoryInfo or not

                CmdP.CommandText = "SELECT TOP 1 FactoryRegNo FROM FactoryInfo WHERE FactoryNameIndex=@FactoryNameIndex AND FactoryName=@FactoryName";
                CmdP.Parameters.Clear();
                CmdP.Parameters.AddWithValue("@FactoryNameIndex", strComFacBizNameIndex);
                CmdP.Parameters.AddWithValue("@FactoryName", strComFacBizName);
                SqlDataReader drFactoryInfo = CmdP.ExecuteReader();

                string strEmsNoTemp = string.Empty;

                if (drFactoryInfo.Read())
                {
                    Exist = true;
                    strComFacBizType = "1";
                    strAdminNo = drFactoryInfo["FactoryRegNo"].ToString();
                }
                if (drFactoryInfo != null)
                    drFactoryInfo.Close();

                #endregion

                #region Exist in CompanyInfo or not

                if (Exist == false)
                {
                    CmdP.CommandText = "SELECT TOP 1 BusinessAdminNo FROM CompanyInfo WHERE CompanyNameIndex=@CompanyNameIndex AND CompanyName=@CompanyName";
                    CmdP.Parameters.Clear();
                    CmdP.Parameters.AddWithValue("@CompanyNameIndex", strComFacBizNameIndex);
                    CmdP.Parameters.AddWithValue("@CompanyName", strComFacBizName);
                    SqlDataReader drCompanyInfo = CmdP.ExecuteReader();
                    if (drCompanyInfo.Read())
                    {
                        Exist = true;
                        strComFacBizType = "0";
                        strAdminNo = drCompanyInfo["BusinessAdminNo"].ToString();
                    }
                    if (drCompanyInfo != null)
                        drCompanyInfo.Close();
                }

                #endregion

                #region Exist in BusinessInfo or not

                if (Exist == false)
                {
                    CmdP.CommandText = "SELECT TOP 1 BusinessAdminNo FROM BusinessInfo WHERE BusinessNameIndex=@BusinessNameIndex AND BusinessName=@BusinessName";
                    CmdP.Parameters.Clear();
                    CmdP.Parameters.AddWithValue("@BusinessNameIndex", strComFacBizNameIndex);
                    CmdP.Parameters.AddWithValue("@BusinessName", strComFacBizName);
                    SqlDataReader drBusinessInfo = CmdP.ExecuteReader();
                    if (drBusinessInfo.Read())
                    {
                        Exist = true;
                        strComFacBizType = "2";
                        strAdminNo = drBusinessInfo["BusinessAdminNo"].ToString();
                    }
                    if (drBusinessInfo != null)
                        drBusinessInfo.Close();
                }

                #endregion

                #region Exist in ComFacBizInfo or not

                if (Exist == false)
                {
                    CmdP.CommandText = "SELECT TOP 1 AdminNo FROM ComFacBizInfo WHERE" +
                                        " ComFacBizNameIndex = @ComFacBizNameIndex AND" +
                                        " ComFacBizName = @ComFacBizName";
                    CmdP.Parameters.Clear();
                    CmdP.Parameters.AddWithValue("@ComFacBizNameIndex", strComFacBizNameIndex);
                    CmdP.Parameters.AddWithValue("@ComFacBizName", strComFacBizName);
                    SqlDataReader drComFacBizInfo = CmdP.ExecuteReader();
                    if (drComFacBizInfo.Read())
                    {
                        Exist = true;
                        strComFacBizType = "3";
                        strAdminNo = drComFacBizInfo["AdminNo"].ToString();
                    }
                    if (drComFacBizInfo != null)
                        drComFacBizInfo.Close();
                }

                #endregion

                ConnP.Dispose();
                if (ConnP != null)
                    ConnP.Close();
                CmdP.Dispose();
            }
            catch (Exception ex)
            {
                //若發生錯誤則需要將錯誤訊息新增至 DataTransErrorLog。
                funcUtil.ins1stDataTransErrorLog("-", "-", "-", ex.Message,
                                                 "ComFacBizMatch.funcSearchByName()，strComFacBizName：" + strComFacBizName);
                Console.WriteLine("ComFacBizMatch.funcSearchByName()錯誤，" + ex.Message);
            }
            
            if(Exist)
                return strComFacBizType + "," + strAdminNo;
            else
                return "";
        }

        public static string funcSearchForFD(string strComFacBizName, string strComFacBizAddr, string strBusinessAdminNo, string strFactoryRegNo)
        {
            var strComFacBizType = string.Empty;    //暫存 ComFacBizType (0-公司；1-工廠；2-營利事業；3-其他)
            var strAdminNo = string.Empty;          //暫存 AdminNo (0-公司統一編號 / 1-工廠登記證號/ 2-營利事業統一編號/ 3-唯一值，識別用：遞增流水號)
            bool Exist = false;                     //暫存判斷資料搜尋後是否存在

            #region 傳入公司/工廠/營利事業名稱若為「-」或是空白，則回傳無。
            if (strComFacBizName == "-" || string.IsNullOrEmpty(strComFacBizName) || string.IsNullOrWhiteSpace(strComFacBizName))
                return "";

            string strComFacBizNameIndex = "";
            if (strComFacBizName.Length > 1)
                strComFacBizNameIndex = strComFacBizName.Substring(0, 1);
            else
                strComFacBizNameIndex = strComFacBizName;
            #endregion

            strComFacBizAddr = strComFacBizAddr.Substring(0,5);
            

            try
            {
                //因為需要從 Primary DB 取出轉置的資料，需要定義 Primary DB 連線。
                SqlConnection ConnP = new SqlConnection(strPrimaryDBConn);
                SqlCommand CmdP = ConnP.CreateCommand();
                ConnP.Open();
                CmdP.CommandTimeout = 3000;

                #region Exist in FactoryInfo or not

                //CmdP.CommandText = "SELECT TOP 1 FactoryRegNo FROM FactoryInfo WHERE FactoryNameIndex=@FactoryNameIndex AND FactoryName=@FactoryName ";
                CmdP.CommandText = @"SELECT TOP 1 FactoryRegNo FROM FactoryInfo T1 
                                     INNER JOIN CountyZipMapping T2 ON T1.FactoryZipCode = T2.ZipCode
                                     WHERE FactoryNameIndex=@FactoryNameIndex AND FactoryName=@FactoryName AND FactoryRegNo=@FactoryRegNo AND CONCAT(T2.ZipLocalName,T1.FactoryAddress) like '%"+ strComFacBizAddr + "%'";
                CmdP.Parameters.Clear();
                CmdP.Parameters.AddWithValue("@FactoryNameIndex", strComFacBizNameIndex);
                CmdP.Parameters.AddWithValue("@FactoryName", strComFacBizName);
                CmdP.Parameters.AddWithValue("@FactoryRegNo", strFactoryRegNo);
                SqlDataReader drFactoryInfo = CmdP.ExecuteReader();

                string strEmsNoTemp = string.Empty;

                if (drFactoryInfo.Read())
                {
                    Exist = true;
                    strComFacBizType = "1";
                    strAdminNo = drFactoryInfo["FactoryRegNo"].ToString();
                }
                if (drFactoryInfo != null)
                    drFactoryInfo.Close();

                #endregion

                #region Exist in CompanyInfo or not

                if (Exist == false)
                {
                    //CmdP.CommandText = "SELECT TOP 1 BusinessAdminNo FROM CompanyInfo WHERE CompanyNameIndex=@CompanyNameIndex AND CompanyName=@CompanyName ";
                    CmdP.CommandText = @"SELECT TOP 1 BusinessAdminNo FROM CompanyInfo T1 
                                         INNER JOIN CountyZipMapping T2 ON T1.CompanyZipCode = T2.ZipCode
                                         WHERE CompanyNameIndex = @CompanyNameIndex AND CompanyName = @CompanyName AND BusinessAdminNo = @BusinessAdminNo AND CONCAT(T2.ZipLocalName,T1.CompanyAddress) like '%"+ strComFacBizAddr + "%'";
                    CmdP.Parameters.Clear();
                    CmdP.Parameters.AddWithValue("@CompanyNameIndex", strComFacBizNameIndex);
                    CmdP.Parameters.AddWithValue("@CompanyName", strComFacBizName);
                    CmdP.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                    SqlDataReader drCompanyInfo = CmdP.ExecuteReader();
                    if (drCompanyInfo.Read())
                    {
                        Exist = true;
                        strComFacBizType = "0";
                        strAdminNo = drCompanyInfo["BusinessAdminNo"].ToString();
                    }
                    if (drCompanyInfo != null)
                        drCompanyInfo.Close();
                }

                #endregion

                #region Exist in BusinessInfo or not

                if (Exist == false)
                {
                    //CmdP.CommandText = "SELECT TOP 1 BusinessAdminNo FROM BusinessInfo WHERE BusinessNameIndex=@BusinessNameIndex AND BusinessName=@BusinessName";

                    CmdP.CommandText = @"SELECT TOP 1 BusinessAdminNo FROM BusinessInfo T1 
                                         INNER JOIN CountyZipMapping T2 ON T1.BusinessZipCode = T2.ZipCode
                                         WHERE BusinessNameIndex = @BusinessNameIndex AND BusinessName = @BusinessName AND BusinessAdminNo = @BusinessAdminNo AND CONCAT(T2.ZipLocalName,T1.BusinessAddress) like '%"+ strComFacBizAddr + "%'";
                    CmdP.Parameters.Clear();
                    CmdP.Parameters.AddWithValue("@BusinessNameIndex", strComFacBizNameIndex);
                    CmdP.Parameters.AddWithValue("@BusinessName", strComFacBizName);
                    CmdP.Parameters.AddWithValue("@BusinessAdminNo", strBusinessAdminNo);
                    SqlDataReader drBusinessInfo = CmdP.ExecuteReader();
                    if (drBusinessInfo.Read())
                    {
                        Exist = true;
                        strComFacBizType = "2";
                        strAdminNo = drBusinessInfo["BusinessAdminNo"].ToString();
                    }
                    if (drBusinessInfo != null)
                        drBusinessInfo.Close();
                }

                #endregion

                #region Exist in ComFacBizInfo or not

                if (Exist == false)
                {
                    CmdP.CommandText = "SELECT TOP 1 AdminNo FROM ComFacBizInfo WHERE" +
                                        " ComFacBizNameIndex = @ComFacBizNameIndex AND" +
                                        " ComFacBizName = @ComFacBizName";
                    CmdP.Parameters.Clear();
                    CmdP.Parameters.AddWithValue("@ComFacBizNameIndex", strComFacBizNameIndex);
                    CmdP.Parameters.AddWithValue("@ComFacBizName", strComFacBizName);
                    SqlDataReader drComFacBizInfo = CmdP.ExecuteReader();
                    if (drComFacBizInfo.Read())
                    {
                        Exist = true;
                        strComFacBizType = "3";
                        strAdminNo = drComFacBizInfo["AdminNo"].ToString();
                    }
                    if (drComFacBizInfo != null)
                        drComFacBizInfo.Close();
                }

                #endregion

                ConnP.Dispose();
                if (ConnP != null)
                    ConnP.Close();
                CmdP.Dispose();
            }
            catch (Exception ex)
            {
                //若發生錯誤則需要將錯誤訊息新增至 DataTransErrorLog。
                funcUtil.ins1stDataTransErrorLog("-", "-", "-", ex.Message,
                                                 "ComFacBizMatch.funcSearchByName()，strComFacBizName：" + strComFacBizName);
                Console.WriteLine("ComFacBizMatch.funcSearchByName()錯誤，" + ex.Message);
            }

            if (Exist)
                return strComFacBizType + "," + strAdminNo;
            else
                return "";
        }
        /// <summary>
        /// 取得 DepartmentMapping 中，部會代碼(SDeptId)及 TempTableName 資料。
        /// </summary>
        public static void getDepartmentMapping()
        {
            if (dicDepartmentMapping.Count == 0)
            {
                try
                {
                    SqlConnection Conn = new SqlConnection(strPrimaryDBConn);
                    SqlCommand Cmd = Conn.CreateCommand();
                    Conn.Open();
                    Cmd.CommandText = "SELECT DISTINCT SDeptId, TempTableName FROM DepartmentMapping";
                    SqlDataReader dr = Cmd.ExecuteReader();
                    dicDepartmentMapping.Clear();
                    while (dr.Read())
                    {
                        dicDepartmentMapping.Add(dr["TempTableName"].ToString(), dr["SDeptId"].ToString());
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
                    //若發生錯誤則需要將錯誤訊息新增至 DataTransErrorLog。
                    funcUtil.ins1stDataTransErrorLog("-", "-", "-", ex.Message, "getDepartmentMapping");
                    Console.WriteLine("getDepartmentMapping錯誤，" + ex.Message);
                }
            }
        }

        /// <summary>
        /// 地址正規化
        /// </summary>
        /// <param name="strAddress">地址</param>
        /// <returns></returns>
        public static string AddressAlignment(string strAddress)
        {
            try
            {
                //若無地址則跳出不進行處理
                if (strAddress == null)
                    return strAddress;
                if (strAddress.Trim().Equals("-") || strAddress.Trim().Equals(""))
                    return strAddress.Trim();

                //去除「空白」
                strAddress = strAddress.Replace(" ", "").Replace("　", "");

                //將「半形字」轉換成「全形字」
                char[] c = strAddress.ToCharArray();
                for (int i = 0; i < c.Length; i++)
                {
                    if (c[i] >= 33 && c[i] <= 126)
                        c[i] = (char)(c[i] + 65248);
                }
                strAddress = new string(c);

                //將「英文小寫」轉換成「英文大寫」
                strAddress = strAddress.ToUpper();

                //去除「開頭郵遞區號數字」
                Regex rgx = new Regex("^[\\d]+");
                strAddress = rgx.Replace(strAddress, "");

                //去除「開頭及結尾的括號內容」
                for (int i = 0; i < 5; i++)
                {
                    rgx = new Regex("^[〈【﹝（︵][^〉】﹞）︶]*[〉】﹞）︶]");
                    strAddress = rgx.Replace(strAddress, "");
                    rgx = new Regex("[〈【﹝（︵][^〈【﹝（︵]*[〉】﹞）︶]$");
                    strAddress = rgx.Replace(strAddress, "");
                }

                //將「阿拉伯數字(0,1,2,3,...)」轉換成「中文數字(零,一,二,三,...)」
                strAddress = strAddress.Replace("０", "零").Replace("○", "零");
                strAddress = strAddress.Replace("１", "一");
                strAddress = strAddress.Replace("２", "二");
                strAddress = strAddress.Replace("３", "三");
                strAddress = strAddress.Replace("４", "四");
                strAddress = strAddress.Replace("５", "五");
                strAddress = strAddress.Replace("６", "六");
                strAddress = strAddress.Replace("７", "七");
                strAddress = strAddress.Replace("８", "八");
                strAddress = strAddress.Replace("９", "九");

                //將「-」轉換成「之」
                strAddress = strAddress.Replace("－", "之");

                //將「F」轉換成「樓」
                strAddress = strAddress.Replace('F', '樓').Replace("Ｆ", "樓");

                //將「台」轉換成「臺」
                strAddress = strAddress.Replace('台', '臺');
            }
            catch (Exception ex)
            {
                Console.WriteLine("地址處理失敗，" + ex.Message);
                throw;
            }

            return strAddress;
        }
    }
}