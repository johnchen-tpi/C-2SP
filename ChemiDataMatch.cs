using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace Normalization_MS
{
    class ChemiDataMatch
    {
        public static string strPrimaryDBConn = ConfigurationManager.AppSettings["PrimaryDBConnectionStr"];     //ChemiPrimary DB 連限字串
        public static string strBaseDBConn = ConfigurationManager.AppSettings["BaseDBConnectionStr"];           //ChemiBase DB 連限字串
        private static DataTable dtChemicalData = new DataTable();      //用來暫存回傳的化學物質資料

        ///// <summary>
        ///// 確認是否需要重新轉置所有化學物質基本資料。
        ///// (在執行化學物質轉置前需要先執行本函式)
        ///// </summary>
        //public static bool funcCheckMatch()
        //{
        //    bool blReMerge = false;

        //    string strChemiDataMatchCountTemp = ""; //Config 表中的 ConfigValue
        //    string strChemiDataMatchCount = "";     //ChemiDataMatch 表中的 總數

        //    //因為需要從 Base DB 取出轉置的資料，需要定義 Base DB 連線。
        //    SqlConnection ConnB = new SqlConnection(strBaseDBConn);
        //    SqlCommand CmdB = ConnB.CreateCommand();
        //    ConnB.Open();
        //    CmdB.CommandTimeout = 3000;

        //    try
        //    {
        //        CmdB.CommandText = "SELECT ConfigValue FROM Config WHERE ConfigName='ChemiDataMatchCount'";
        //        SqlDataReader dr1 = CmdB.ExecuteReader();
        //        if (dr1.Read())
        //        {
        //            strChemiDataMatchCountTemp = dr1["ConfigValue"].ToString().Trim();
        //        }
        //        if (dr1 != null)
        //            dr1.Close();

        //        CmdB.CommandText = "SELECT count(*) AS ChemiDataMatchCount FROM ChemiDataMatch";
        //        SqlDataReader dr2 = CmdB.ExecuteReader();
        //        if (dr2.Read())
        //        {
        //            strChemiDataMatchCount = dr2["ChemiDataMatchCount"].ToString().Trim();
        //        }
        //        if (dr2 != null)
        //            dr2.Close();

        //        //若有異動(總數與Config中不一致)則需要將所有化學物質進行對應表重新整併。
        //        if (strChemiDataMatchCountTemp != strChemiDataMatchCount)
        //        {
        //            blReMerge = true;   //需要重新 Merge

        //            SqlConnection ConnP = new SqlConnection(strPrimaryDBConn);
        //            SqlCommand CmdP = ConnP.CreateCommand();
        //            ConnP.Open();
        //            CmdP.CommandTimeout = 3000;

        //            try
        //            {
        //                CmdB.CommandText = "UPDATE Config SET ConfigValue='" + strChemiDataMatchCount + "' WHERE ConfigName='ChemiDataMatchCount'";
        //                CmdB.ExecuteNonQuery();
        //            }
        //            catch (Exception ex)
        //            {
        //                Console.WriteLine(ex.ToString());
        //                funcUtil.ins2ndDataTransErrorLog("-", "2ndAP_getChemicalSN", "-", ex.Message, "funcCheckMatch()，1stUpdate。");
        //            }
        //            finally
        //            {
        //                ConnP.Dispose();
        //                if (ConnP != null)
        //                    ConnP.Close();
        //                CmdP.Dispose();
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine(ex.ToString());
        //        funcUtil.ins2ndDataTransErrorLog("-", "2ndAP_getChemicalSN", "-", ex.Message, "funcCheckMatch()，2ndUpdate。");
        //    }
        //    finally
        //    {
        //        ConnB.Dispose();
        //        if (ConnB != null)
        //            ConnB.Close();
        //        CmdB.Dispose();
        //    }

        //    return blReMerge;
        //}


//        /// <summary>
//        /// 取得整併後的化學物質資料
//        /// </summary>
//        /// <param name="strChemicalEngName">化學物質英文名稱</param>
//        /// <param name="strChemicalChnName">化學物質中文名稱</param>
//        /// <param name="strCASNo">化學物質 CASNo</param>
//        /// <param name="strTransId">交易編號DataId+yyMMddHHmmssfff</param>
//        /// <param name="strPrimaryTableName">未合併前的來源資料表</param>
//        /// <param name="strNo">記Temp DB XML的筆數(流水號)</param>
//        /// <returns>化學物質欄位資料(DataTable)</returns>
//        public static DataTable getChemicalData(string strChemicalEngName, string strChemicalChnName, string strCASNo,
//                                                string strTransId, string strPrimaryTableName, string strNo)
//        {
//            bool blFlag = true;         //本程序是否有正常執行(true-是；false-否)
//            bool Exist = false;         //暫存判斷化學物質整併表是否存在資料
//            string strChemiEngNameMatch = "";   //暫存化學物質整併表英文名稱
//            string strChemiChnNameMatch = "";   //暫存化學物質整併表中文名稱
//            string strCASNoMatch = "";          //暫存化學物質整併表 CasNo
//            string strChemiEngAliases = "";     //暫存化學物質英文別名
//            string strChemiChnAliases = "";     //暫存化學物質中文別名
//            string strCCCCodeMatch = "";        //暫存化學物質整併表 CCCCode

//            dtChemicalData.Reset();     //初始化，每次呼叫本函式先清空回傳的暫存值

//            if (strChemicalChnName == "-" && strChemicalEngName == "-" && strCASNo == "-")
//                return dtChemicalData;
//            if (strChemicalChnName == "" && strChemicalEngName == "" && strCASNo == "")
//                return dtChemicalData;

//            #region 查詢化學物質整併表是否有資料。
//            SqlConnection Conn = new SqlConnection(strBaseDBConn);
//            SqlCommand Cmd = Conn.CreateCommand();
//            Conn.Open();
//            Cmd.CommandTimeout = 3000;
//            try
//            {
//                Cmd.CommandText = "SELECT SeqNo, ChemicalEngName, ChemicalChnName, CASNo, ChemiEngNameMatch, ChemiChnNameMatch, CASNoMatch, ChemiEngAliases, ChemiChnAliases, CCCCode" +
//                                  " FROM ChemiDataMatch" +
//                                  " WHERE ChemicalEngName=@ChemicalEngName AND ChemicalChnName=@ChemicalChnName AND CASNo=@CASNo ORDER BY SeqNo DESC";
//                Cmd.Parameters.Clear();
//                Cmd.Parameters.AddWithValue("@ChemicalEngName", strChemicalEngName);
//                Cmd.Parameters.AddWithValue("@ChemicalChnName", strChemicalChnName);
//                Cmd.Parameters.AddWithValue("@CASNo", strCASNo);
//                SqlDataReader dr = Cmd.ExecuteReader();
//                if (dr.Read())
//                {
//                    Exist = true;
//                    strChemiEngNameMatch = dr["ChemiEngNameMatch"].ToString();
//                    strChemiChnNameMatch = dr["ChemiChnNameMatch"].ToString();
//                    strCASNoMatch = dr["CASNoMatch"].ToString();
//                    strChemiEngAliases = dr["ChemiEngAliases"].ToString();
//                    strChemiChnAliases = dr["ChemiChnAliases"].ToString();
//                    strCCCCodeMatch = dr["CCCCode"].ToString();
//                }
//                if (dr != null)
//                    dr.Close();
//            }
//            catch (Exception ex)
//            {
//                blFlag = false;
//                Console.WriteLine(ex.ToString());
//                funcUtil.ins2ndDataTransErrorLog("-", "2ndAP_getChemicalSN",
//                                                 "-", "取出對應表資料失敗，" + ex.Message, "strChemicalEngName:" + strChemicalEngName +
//                                                 ";strChemicalChnName:" + strChemicalChnName + ";strCASNo:" + strCASNo);
//            }
//            finally
//            {
//                Conn.Dispose();
//                if (Conn != null)
//                    Conn.Close();
//                Cmd.Dispose();
//            }
//            #endregion

//            //本程序有正常執行才進行以下相關轉置動作。
//            if (blFlag)
//            {
//                //查詢傳入的化學物質於對應表是否有資料。
//                if (Exist)
//                {
//                    #region 表示對應表「有」資料，接下來判定「化學物質資料(ChemicalData)」總表是否已經存在。
//                    //================================================================================
//                    //表示對應表「有」資料，接下來判定「化學物質資料(ChemicalData)」總表是否已經存在。
//                    //================================================================================
//                    SqlConnection ConnP = new SqlConnection(strPrimaryDBConn);
//                    SqlCommand CmdP = ConnP.CreateCommand();
//                    ConnP.Open();
//                    CmdP.CommandTimeout = 3000;
//                    CmdP.CommandText = @"SELECT
//	                                        *, 'D' AS DelField
//                                        FROM
//	                                        ChemicalData
//                                        WHERE
//	                                        (
//		                                        ChemicalEngName = @ChemicalEngName
//		                                        AND ChemicalChnName = @ChemicalChnName
//		                                        AND CASNo = @CASNo
//	                                        )
//                                        UNION
//                                        SELECT
//	                                        *, 'T' AS DelField
//                                        FROM
//	                                        ChemicalData
//                                        WHERE
//                                        (
//	                                        ChemicalEngName = @ChemiEngNameMatch
//	                                        AND ChemicalChnName = @ChemiChnNameMatch
//	                                        AND CASNo = @CASNoMatch
//                                        )";
//                    CmdP.Parameters.Clear();
//                    CmdP.Parameters.AddWithValue("@ChemicalEngName", strChemicalEngName);
//                    CmdP.Parameters.AddWithValue("@ChemicalChnName", strChemicalChnName);
//                    CmdP.Parameters.AddWithValue("@CASNo", strCASNo);
//                    CmdP.Parameters.AddWithValue("@ChemiEngNameMatch", strChemiEngNameMatch);
//                    CmdP.Parameters.AddWithValue("@ChemiChnNameMatch", strChemiChnNameMatch);
//                    CmdP.Parameters.AddWithValue("@CASNoMatch", strCASNoMatch);
//                    SqlDataAdapter drP = new SqlDataAdapter(CmdP);
//                    try
//                    {
//                        drP.Fill(dtChemicalData);
//                        if (dtChemicalData.Rows.Count > 1)
//                        {
//                            if (dtChemicalData.Rows[0]["ChemicalSN"].ToString() == dtChemicalData.Rows[1]["ChemicalSN"].ToString())
//                            {
//                                dtChemicalData.Rows.RemoveAt(1);
//                            }
//                        }

//                        DataTableReader SqlDR = new DataTableReader(new[] { dtChemicalData });
//                        if (!SqlDR.HasRows)
//                        {
//                            //理論上只會發生在還未轉置化學物質，但已經在對應表建立資料。
//                            #region 對應表「有」資料，但是不存在於「化學物質資料(ChemicalData)」總表。
//                            //不存在於「化學物質資料(ChemicalData)」總表，需要新增該筆化學物質資料(只先塞中英文名稱)。
//                            CmdP.CommandText = "INSERT INTO ChemicalData" +
//                                            " (ChemicalEngName, ChemicalChnName, CASNo, TransId, PrimaryTableName, No, ChemiEngAliases, ChemiChnAliases, CCCCode)" +
//                                            " VALUES (@ChemicalEngName, @ChemicalChnName, @CASNo, @TransId, @PrimaryTableName, @No, @ChemiEngAliases, @ChemiChnAliases, @CCCCode)";
//                            CmdP.Parameters.Clear();
//                            CmdP.Parameters.AddWithValue("@ChemicalEngName", strChemiEngNameMatch);
//                            CmdP.Parameters.AddWithValue("@ChemicalChnName", strChemiChnNameMatch);
//                            CmdP.Parameters.AddWithValue("@CASNo", strCASNoMatch);
//                            CmdP.Parameters.AddWithValue("@TransId", strTransId);
//                            CmdP.Parameters.AddWithValue("@PrimaryTableName", strPrimaryTableName);
//                            CmdP.Parameters.AddWithValue("@No", strNo);
//                            CmdP.Parameters.AddWithValue("@ChemiEngAliases", strChemiEngAliases);
//                            CmdP.Parameters.AddWithValue("@ChemiChnAliases", strChemiChnAliases);
//                            CmdP.Parameters.AddWithValue("@CCCCode", strCCCCodeMatch);
//                            CmdP.ExecuteNonQuery();

//                            //插入完畢後取出該筆化學物質
//                            CmdP.CommandText = "SELECT * FROM ChemicalData" +
//                                               " WHERE ChemicalEngName=@ChemicalEngName AND ChemicalChnName=@ChemicalChnName AND CASNo=@CASNo";
//                            CmdP.Parameters.Clear();
//                            CmdP.Parameters.AddWithValue("@ChemicalEngName", strChemiEngNameMatch);
//                            CmdP.Parameters.AddWithValue("@ChemicalChnName", strChemiChnNameMatch);
//                            CmdP.Parameters.AddWithValue("@CASNo", strCASNoMatch);
//                            drP.Fill(dtChemicalData);
//                            #endregion
//                        }
//                        else
//                        {
//                            //存在於「化學物質資料(ChemicalData)」總表。
//                            if (dtChemicalData.Rows.Count > 1)
//                            {
//                                #region !!!!!!!! 存在於「化學物質資料(ChemicalData)」總表，卻超過 1 筆，需要做化學物質整併。 !!!!!!!!
//                                funcMergeChemicalData(strChemicalEngName, strChemicalChnName, strCASNo, CmdP);
//                                #endregion
//                            }
//                            else if (dtChemicalData.Rows.Count == 1)
//                            {
//                                #region !!!!!!!! 存在於「化學物質資料(ChemicalData)」總表，只有 1 筆，但卻與整併表資料不一致，需要做化學物質整併。 !!!!!!!!
//                                if (!(strChemiEngNameMatch == dtChemicalData.Rows[0]["ChemicalEngName"].ToString() &&
//                                      strChemiChnNameMatch == dtChemicalData.Rows[0]["ChemicalChnName"].ToString() &&
//                                      strCASNoMatch == dtChemicalData.Rows[0]["CASNo"].ToString() &&
//                                      strChemiEngNameMatch == dtChemicalData.Rows[0]["ChemiEngNameRev"].ToString() &&
//                                      strChemiChnNameMatch == dtChemicalData.Rows[0]["ChemiChnNameRev"].ToString() &&
//                                      strChemiEngAliases == dtChemicalData.Rows[0]["ChemiEngAliases"].ToString() &&
//                                      strChemiChnAliases == dtChemicalData.Rows[0]["ChemiChnAliases"].ToString()))
//                                {
//                                    CmdP.CommandText = "UPDATE ChemicalData SET ChemicalEngName=@ChemicalEngName," +
//                                                       " ChemicalChnName=@ChemicalChnName, CASNo=@CASNo," +
//                                                       " ChemiEngNameRev=@ChemiEngNameRev, ChemiChnNameRev=@ChemiChnNameRev," +
//                                                       " TransId=@TransId, PrimaryTableName=@PrimaryTableName, No=@No," +
//                                                       " ChemiEngAliases=@ChemiEngAliases, ChemiChnAliases=@ChemiChnAliases";
//                                    if (!(string.IsNullOrEmpty(strCCCCodeMatch) || string.IsNullOrWhiteSpace(strCCCCodeMatch)))
//                                        CmdP.CommandText += ", CCCCode=@CCCCode";
//                                    CmdP.CommandText += " WHERE ChemicalSN=@ChemicalSN";
//                                    CmdP.Parameters.Clear();
//                                    CmdP.Parameters.AddWithValue("@ChemicalEngName", strChemiEngNameMatch);
//                                    CmdP.Parameters.AddWithValue("@ChemicalChnName", strChemiChnNameMatch);
//                                    CmdP.Parameters.AddWithValue("@CASNo", strCASNoMatch);
//                                    CmdP.Parameters.AddWithValue("@ChemiEngNameRev", strChemiEngNameMatch);
//                                    CmdP.Parameters.AddWithValue("@ChemiChnNameRev", strChemiChnNameMatch);
//                                    CmdP.Parameters.AddWithValue("@TransId", strTransId);
//                                    CmdP.Parameters.AddWithValue("@PrimaryTableName", strPrimaryTableName);
//                                    CmdP.Parameters.AddWithValue("@No", strNo);
//                                    CmdP.Parameters.AddWithValue("@ChemiEngAliases", strChemiEngAliases);
//                                    CmdP.Parameters.AddWithValue("@ChemiChnAliases", strChemiChnAliases);
//                                    if (!(string.IsNullOrEmpty(strCCCCodeMatch) || string.IsNullOrWhiteSpace(strCCCCodeMatch)))
//                                        CmdP.Parameters.AddWithValue("@CCCCode", strCCCCodeMatch);
//                                    CmdP.Parameters.AddWithValue("@ChemicalSN", dtChemicalData.Rows[0]["ChemicalSN"].ToString());
//                                    CmdP.ExecuteNonQuery();

//                                    //重新指定回傳的相關值
//                                    dtChemicalData.Rows[0]["ChemicalEngName"] = strChemiEngNameMatch;
//                                    dtChemicalData.Rows[0]["ChemicalChnName"] = strChemiChnNameMatch;
//                                    dtChemicalData.Rows[0]["CASNo"] = strCASNoMatch;
//                                    dtChemicalData.Rows[0]["ChemiEngNameRev"] = strChemiEngNameMatch;
//                                    dtChemicalData.Rows[0]["ChemiChnNameRev"] = strChemiChnNameMatch;
//                                    dtChemicalData.Rows[0]["ChemiEngAliases"] = strChemiEngAliases;
//                                    dtChemicalData.Rows[0]["ChemiChnAliases"] = strChemiChnAliases;
//                                    if (!(string.IsNullOrEmpty(strCCCCodeMatch) || string.IsNullOrWhiteSpace(strCCCCodeMatch)))
//                                        dtChemicalData.Rows[0]["CCCCode"] = strCCCCodeMatch;
//                                }
//                                #endregion
//                            }
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        Console.WriteLine(ex.ToString());
//                        funcUtil.ins2ndDataTransErrorLog("-", "2ndAP_getChemicalSN",
//                                                         "-", "化學物質整併失敗，" + ex.Message, "strChemicalEngName:" + strChemicalEngName +
//                                                         ";strChemicalChnName:" + strChemicalChnName + ";strCASNo:" + strCASNo);
//                    }
//                    finally
//                    {
//                        drP.Dispose();
//                        ConnP.Dispose();
//                        if (ConnP != null)
//                            ConnP.Close();
//                        CmdP.Dispose();
//                    }
//                    #endregion
//                }
//                else
//                {
//                    #region 表示對應表「無」資料，則先查詢「化學物質資料(ChemicalData)」總表是否有該筆資料，無則直接新增該筆資料。
//                    //=====================================================================================================
//                    //表示對應表「無」資料，則先查詢「化學物質資料(ChemicalData)」總表是否有該筆資料，無則直接新增該筆資料。
//                    //=====================================================================================================
//                    if (!(String.IsNullOrWhiteSpace(strChemicalEngName) && String.IsNullOrWhiteSpace(strChemicalChnName) && String.IsNullOrWhiteSpace(strCASNo)))
//                    {
//                        SqlConnection ConnP = new SqlConnection(strPrimaryDBConn);
//                        SqlCommand CmdP = ConnP.CreateCommand();
//                        ConnP.Open();
//                        CmdP.CommandTimeout = 3000;

//                        CmdP.CommandText = "SELECT * FROM ChemicalData" +
//                                           " WHERE ChemicalEngName=@ChemicalEngName AND ChemicalChnName=@ChemicalChnName AND CASNo=@CASNo";
//                        CmdP.Parameters.Clear();
//                        CmdP.Parameters.AddWithValue("@ChemicalEngName", strChemicalEngName);
//                        CmdP.Parameters.AddWithValue("@ChemicalChnName", strChemicalChnName);
//                        CmdP.Parameters.AddWithValue("@CASNo", strCASNo);
//                        SqlDataAdapter drP = new SqlDataAdapter(CmdP);

//                        try
//                        {
//                            drP.Fill(dtChemicalData);
//                            DataTableReader SqlDR = new DataTableReader(new[] { dtChemicalData });
//                            if (!SqlDR.HasRows)
//                            {
//                                //不存在於「化學物質資料(ChemicalData)」總表，需要新增該筆化學物質資料(只先塞中英文名稱及CASNo)。
//                                CmdP.CommandText = "INSERT INTO ChemicalData" +
//                                                    " (ChemicalEngName, ChemicalChnName, CASNo, TransId, PrimaryTableName, No, ChemiEngAliases, ChemiChnAliases)" +
//                                                    " VALUES (@ChemicalEngName, @ChemicalChnName, @CASNo, @TransId, @PrimaryTableName, @No, @ChemiEngAliases, @ChemiChnAliases)";
//                                CmdP.Parameters.Clear();
//                                CmdP.Parameters.AddWithValue("@ChemicalEngName", strChemicalEngName);
//                                CmdP.Parameters.AddWithValue("@ChemicalChnName", strChemicalChnName);
//                                CmdP.Parameters.AddWithValue("@CASNo", strCASNo);
//                                CmdP.Parameters.AddWithValue("@TransId", strTransId);
//                                CmdP.Parameters.AddWithValue("@PrimaryTableName", strPrimaryTableName);
//                                CmdP.Parameters.AddWithValue("@No", strNo);
//                                CmdP.Parameters.AddWithValue("@ChemiEngAliases", strChemiEngAliases);
//                                CmdP.Parameters.AddWithValue("@ChemiChnAliases", strChemiChnAliases);
//                                CmdP.ExecuteNonQuery();

//                                //插入完畢後取出該筆化學物質
//                                CmdP.CommandText = "SELECT * FROM ChemicalData" +
//                                               " WHERE ChemicalEngName=@ChemicalEngName AND ChemicalChnName=@ChemicalChnName AND CASNo=@CASNo";
//                                CmdP.Parameters.Clear();
//                                CmdP.Parameters.AddWithValue("@ChemicalEngName", strChemicalEngName);
//                                CmdP.Parameters.AddWithValue("@ChemicalChnName", strChemicalChnName);
//                                CmdP.Parameters.AddWithValue("@CASNo", strCASNo);
//                                drP.Fill(dtChemicalData);
//                            }
//                        }
//                        catch (Exception ex)
//                        {
//                            Console.WriteLine(ex.ToString());
//                            funcUtil.ins2ndDataTransErrorLog("-", "2ndAP_getChemicalSN",
//                                                             "-", "新增化學物質失敗，" + ex.Message, "strChemicalEngName:" + strChemicalEngName +
//                                                             ";strChemicalChnName:" + strChemicalChnName + ";strCASNo:" + strCASNo);
//                        }
//                        finally
//                        {
//                            drP.Dispose();
//                            ConnP.Dispose();
//                            if (ConnP != null)
//                                ConnP.Close();
//                            CmdP.Dispose();
//                        }
//                    }
//                    #endregion
//                }
//            }

//            return dtChemicalData;
//        }


        ///// <summary>
        ///// 化學物質整併核心涵式，目前影響的表格有：
        ///// 1. 化學物質與公司對應表(ChemiComMapping) / Update
        ///// 2. 化學物質上下游廠商勾稽統計資料(ChemiTradeStats) / Update
        ///// 3. 食品業者比對統計資料(FdaComFacStats) / Update
        ///// 4. 證件篩選統計資料(CredentialStats) / Update
        ///// 5. 化學物質與產品對應表(ProductChemiMapping) / Update
        ///// 6. 證件資料(ChemiCredential) / Update
        ///// 7. 多元篩選結果報表(MultiSelResultRpt) / Update
        ///// 8. 經濟部工廠食品前端比對統計資料(MOEAComFacStats) / Update
        ///// 9. ...
        ///// Final. 化學物質資料(ChemicalData) / DELETE (移除重複性資料)
        ///// ...
        ///// ...未來若有新增與 ChemicalSN 有關聯的表格需要額外新增涵式...
        ///// ...
        ///// </summary>
        ///// <param name="strChemicalEngName">化學物質英文名稱</param>
        ///// <param name="strChemicalChnName">化學物質中文名稱</param>
        ///// <param name="strCASNo">化學物質 CASNo</param>
        ///// <param name="CmdP">ChemiPrimary 的 Command</param>
        //public static void funcMergeChemicalData(string strChemicalEngName, string strChemicalChnName, string strCASNo,  SqlCommand CmdP)
        //{
        //    //存在於「化學物質資料(ChemicalData)」總表，但數量卻超過一筆，表示有重複資料。
        //    //理論上會有 2 筆，超過 2 筆代表本程式中的 getChemicalData() 有 Bug(原因不明)，先以刪除解決此Bug。
        //    //一筆是 化學物質資料(ChemicalData) 的 ChemicalEngName/ChemicalChnName/CASNo Index 資料(Primary Key)。
        //    //另一筆是 化學物質整併表(ChemiDataMatch) 取出的 ChemicalEngName/ChemicalChnName/CASNo 資料。
        //    string strChemicalSN = "";      //正確的 ChemicalSN
        //    string strDelChemicalSN = "";   //欲刪除的 ChemicalSN

        //    try
        //    {
        //        //取得 對應後正確的 ChemicalSN 及 欲刪除的 ChemicalSN
        //        for (int i = 0; i < dtChemicalData.Rows.Count; i++)
        //        {
        //            if (dtChemicalData.Rows[i]["DelField"].ToString() == "D")
        //            {
        //                strDelChemicalSN = strDelChemicalSN + dtChemicalData.Rows[i]["ChemicalSN"].ToString() + ",";
        //            }
        //            else
        //                strChemicalSN = strChemicalSN + dtChemicalData.Rows[i]["ChemicalSN"].ToString() + ",";
        //        }

        //        //去除最後一個「,」
        //        strDelChemicalSN = strDelChemicalSN.Substring(0, strDelChemicalSN.Length - 1);
        //        strChemicalSN = strChemicalSN.Substring(0, strChemicalSN.Length - 1);

        //        //理論上 對應後正確的 ChemicalSN 只會有一個，超過一個就是有 Bug。
        //        if (strChemicalSN.Split(',').Length != 1)
        //        {
        //            dtChemicalData.Clear();
        //            return;
        //        }

        //        for (int i = 0; i < strDelChemicalSN.Split(',').Length; i++)
        //        {
        //            try
        //            {
        //                //Step 1：將「化學物質與公司對應表(ChemiComMapping)」重複性化學物質資料更新為正確的 ChemicalSN。
        //                CmdP.CommandText = "UPDATE ChemiComMapping SET ChemicalSN=@NewChemicalSN WHERE ChemicalSN=@ChemicalSN";
        //                CmdP.Parameters.Clear();
        //                CmdP.Parameters.AddWithValue("@NewChemicalSN", strChemicalSN);
        //                CmdP.Parameters.AddWithValue("@ChemicalSN", strDelChemicalSN.Split(',')[i]);
        //                CmdP.ExecuteNonQuery();
        //            }
        //            catch (Exception ex)
        //            {
        //                //Do Nothing.表示已存在，就直接刪除即可。
        //            }

        //            //Step 2：將「化學物質上下游廠商勾稽統計資料(ChemiTradeStats)」重複性化學物質資料更新為正確的 ChemicalSN。
        //            CmdP.CommandText = "UPDATE ChemiTradeStats SET ChemicalSN=@NewChemicalSN";
        //            if (!String.IsNullOrWhiteSpace(dtChemicalData.Rows[i]["ChemiEngNameRev"].ToString()))
        //                CmdP.CommandText += ", ChemiEngNameRev=@ChemiEngNameRev";
        //            if (!String.IsNullOrWhiteSpace(dtChemicalData.Rows[i]["ChemiChnNameRev"].ToString()))
        //                CmdP.CommandText += ", ChemiChnNameRev=@ChemiChnNameRev";
        //            CmdP.CommandText += " WHERE ChemicalSN=@ChemicalSN";
        //            CmdP.Parameters.Clear();
        //            CmdP.Parameters.AddWithValue("@NewChemicalSN", strChemicalSN);
        //            if (!String.IsNullOrWhiteSpace(dtChemicalData.Rows[i]["ChemiEngNameRev"].ToString()))
        //                CmdP.Parameters.AddWithValue("@ChemiEngNameRev", dtChemicalData.Rows[i]["ChemiEngNameRev"].ToString());
        //            if (!String.IsNullOrWhiteSpace(dtChemicalData.Rows[i]["ChemiChnNameRev"].ToString()))
        //                CmdP.Parameters.AddWithValue("@ChemiChnNameRev", dtChemicalData.Rows[i]["ChemiChnNameRev"].ToString());
        //            CmdP.Parameters.AddWithValue("@ChemicalSN", strDelChemicalSN.Split(',')[i]);
        //            CmdP.ExecuteNonQuery();

        //            //Step 3：將「食品業者比對統計資料(FdaComFacStats)」重複性化學物質資料更新為正確的 ChemicalSN。
        //            CmdP.CommandText = "UPDATE FdaComFacStats SET ChemicalSN=@NewChemicalSN";
        //            if (!String.IsNullOrWhiteSpace(dtChemicalData.Rows[i]["ChemiEngNameRev"].ToString()))
        //                CmdP.CommandText += ", ChemiEngNameRev=@ChemiEngNameRev";
        //            if (!String.IsNullOrWhiteSpace(dtChemicalData.Rows[i]["ChemiChnNameRev"].ToString()))
        //                CmdP.CommandText += ", ChemiChnNameRev=@ChemiChnNameRev";
        //            CmdP.CommandText += " WHERE ChemicalSN=@ChemicalSN";
        //            CmdP.Parameters.Clear();
        //            CmdP.Parameters.AddWithValue("@NewChemicalSN", strChemicalSN);
        //            if (!String.IsNullOrWhiteSpace(dtChemicalData.Rows[i]["ChemiEngNameRev"].ToString()))
        //                CmdP.Parameters.AddWithValue("@ChemiEngNameRev", dtChemicalData.Rows[i]["ChemiEngNameRev"].ToString());
        //            if (!String.IsNullOrWhiteSpace(dtChemicalData.Rows[i]["ChemiChnNameRev"].ToString()))
        //                CmdP.Parameters.AddWithValue("@ChemiChnNameRev", dtChemicalData.Rows[i]["ChemiChnNameRev"].ToString());
        //            CmdP.Parameters.AddWithValue("@ChemicalSN", strDelChemicalSN.Split(',')[i]);
        //            CmdP.ExecuteNonQuery();

        //            //Step 4：將「證件篩選統計資料(CredentialStats)」重複性化學物質資料更新為正確的 ChemicalSN。
        //            //CmdP.CommandText = "UPDATE CredentialStats SET ChemicalSN=@NewChemicalSN";
        //            //if (!String.IsNullOrWhiteSpace(dtChemicalData.Rows[i]["ChemiEngNameRev"].ToString()))
        //            //    CmdP.CommandText += ", ChemiEngNameRev=@ChemiEngNameRev";
        //            //if (!String.IsNullOrWhiteSpace(dtChemicalData.Rows[i]["ChemiChnNameRev"].ToString()))
        //            //    CmdP.CommandText += ", ChemiChnNameRev=@ChemiChnNameRev";
        //            //CmdP.CommandText += " WHERE ChemicalSN=@ChemicalSN";
        //            //CmdP.Parameters.Clear();
        //            //CmdP.Parameters.AddWithValue("@NewChemicalSN", strChemicalSN);
        //            //if (!String.IsNullOrWhiteSpace(dtChemicalData.Rows[i]["ChemiEngNameRev"].ToString()))
        //            //    CmdP.Parameters.AddWithValue("@ChemiEngNameRev", dtChemicalData.Rows[i]["ChemiEngNameRev"].ToString());
        //            //if (!String.IsNullOrWhiteSpace(dtChemicalData.Rows[i]["ChemiChnNameRev"].ToString()))
        //            //    CmdP.Parameters.AddWithValue("@ChemiChnNameRev", dtChemicalData.Rows[i]["ChemiChnNameRev"].ToString());
        //            //CmdP.Parameters.AddWithValue("@ChemicalSN", strDelChemicalSN.Split(',')[i]);
        //            //CmdP.ExecuteNonQuery();

        //            try
        //            {
        //                //Step 5：將「化學物質與產品對應表(ProductChemiMapping)」重複性化學物質資料更新為正確的 ChemicalSN。
        //                CmdP.CommandText = "UPDATE ProductChemiMapping SET ChemicalSN=@NewChemicalSN WHERE ChemicalSN=@ChemicalSN";
        //                CmdP.Parameters.Clear();
        //                CmdP.Parameters.AddWithValue("@NewChemicalSN", strChemicalSN);
        //                CmdP.Parameters.AddWithValue("@ChemicalSN", strDelChemicalSN.Split(',')[i]);
        //                CmdP.ExecuteNonQuery();
        //            }
        //            catch (SqlException ex)
        //            {
        //                if (ex.Number == 1062)
        //                {
        //                    CmdP.CommandText = "SELECT ProductSN, ChemicalSN FROM ProductChemiMapping";
        //                    CmdP.CommandText += " WHERE ChemicalSN=@ChemicalSN AND ProductSN IN (";
        //                    CmdP.CommandText += " SELECT ProductSN FROM ProductChemiMapping WHERE ChemicalSN=@NewChemicalSN)";
        //                    CmdP.Parameters.Clear();
        //                    CmdP.Parameters.AddWithValue("@NewChemicalSN", strChemicalSN);
        //                    CmdP.Parameters.AddWithValue("@ChemicalSN", strDelChemicalSN.Split(',')[i]);

        //                    string strProductSNList = "";
        //                    string strChemicalSNList = "";
        //                    SqlDataReader drProductChemiMapping = CmdP.ExecuteReader();
        //                    while (drProductChemiMapping.Read())
        //                    {
        //                        strProductSNList += "," + drProductChemiMapping["ProductSN"].ToString();
        //                        strChemicalSNList += "," + drProductChemiMapping["ChemicalSN"].ToString();
        //                    }
        //                    if (drProductChemiMapping != null)
        //                        drProductChemiMapping.Close();

        //                    for (int delC = 1; delC < strProductSNList.Split(',').Length; delC++)
        //                    {
        //                        CmdP.CommandText = "DELETE FROM ProductChemiMapping WHERE ProductSN=@ProductSN AND ChemicalSN=@ChemicalSN";
        //                        CmdP.Parameters.Clear();
        //                        CmdP.Parameters.AddWithValue("@ProductSN", strProductSNList.Split(',')[delC]);
        //                        CmdP.Parameters.AddWithValue("@ChemicalSN", strChemicalSNList.Split(',')[delC]);
        //                        CmdP.ExecuteNonQuery();
        //                    }

        //                    CmdP.CommandText = "UPDATE ProductChemiMapping SET ChemicalSN=@NewChemicalSN WHERE ChemicalSN=@ChemicalSN";
        //                    CmdP.Parameters.Clear();
        //                    CmdP.Parameters.AddWithValue("@NewChemicalSN", strChemicalSN);
        //                    CmdP.Parameters.AddWithValue("@ChemicalSN", strDelChemicalSN.Split(',')[i]);
        //                    CmdP.ExecuteNonQuery();
        //                }
        //            }

        //            try
        //            {
        //                //Step 6：將「證件資料(ChemiCredential)」重複性化學物質資料更新為正確的 ChemicalSN。
        //                CmdP.CommandText = "UPDATE ChemiCredential SET ChemicalSN=@NewChemicalSN WHERE ChemicalSN=@ChemicalSN";
        //                CmdP.Parameters.Clear();
        //                CmdP.Parameters.AddWithValue("@NewChemicalSN", strChemicalSN);
        //                CmdP.Parameters.AddWithValue("@ChemicalSN", strDelChemicalSN.Split(',')[i]);
        //                CmdP.ExecuteNonQuery();
        //            }
        //            catch (SqlException ex)
        //            {
        //                if (ex.Number == 1062)
        //                {
        //                    CmdP.CommandText = "SELECT ProductSN, ChemicalSN FROM ChemiCredential";
        //                    CmdP.CommandText += " WHERE ChemicalSN=@ChemicalSN AND ProductSN IN (";
        //                    CmdP.CommandText += " SELECT ProductSN FROM ChemiCredential WHERE ChemicalSN=@NewChemicalSN)";
        //                    CmdP.Parameters.Clear();
        //                    CmdP.Parameters.AddWithValue("@NewChemicalSN", strChemicalSN);
        //                    CmdP.Parameters.AddWithValue("@ChemicalSN", strDelChemicalSN.Split(',')[i]);

        //                    string strProductSNList = "";
        //                    string strChemicalSNList = "";
        //                    SqlDataReader drChemiCredential = CmdP.ExecuteReader();
        //                    while (drChemiCredential.Read())
        //                    {
        //                        strProductSNList += "," + drChemiCredential["ProductSN"].ToString();
        //                        strChemicalSNList += "," + drChemiCredential["ChemicalSN"].ToString();
        //                    }
        //                    if (drChemiCredential != null)
        //                        drChemiCredential.Close();

        //                    for (int delC = 1; delC < strProductSNList.Split(',').Length; delC++)
        //                    {
        //                        CmdP.CommandText = "DELETE FROM ChemiCredential WHERE ProductSN=@ProductSN AND ChemicalSN=@ChemicalSN";
        //                        CmdP.Parameters.Clear();
        //                        CmdP.Parameters.AddWithValue("@ProductSN", strProductSNList.Split(',')[delC]);
        //                        CmdP.Parameters.AddWithValue("@ChemicalSN", strChemicalSNList.Split(',')[delC]);
        //                        CmdP.ExecuteNonQuery();
        //                    }

        //                    CmdP.CommandText = "UPDATE ChemiCredential SET ChemicalSN=@NewChemicalSN WHERE ChemicalSN=@ChemicalSN";
        //                    CmdP.Parameters.Clear();
        //                    CmdP.Parameters.AddWithValue("@NewChemicalSN", strChemicalSN);
        //                    CmdP.Parameters.AddWithValue("@ChemicalSN", strDelChemicalSN.Split(',')[i]);
        //                    CmdP.ExecuteNonQuery();
        //                }
        //            }

        //            //Step 7：將「多元篩選結果報表(MultiSelResultRpt)」重複性化學物質資料更新為正確的 ChemicalSN。
        //            CmdP.CommandText = "UPDATE MultiSelResultRpt SET ChemicalSN=@NewChemicalSN WHERE ChemicalSN=@ChemicalSN";
        //            CmdP.Parameters.Clear();
        //            CmdP.Parameters.AddWithValue("@NewChemicalSN", strChemicalSN);
        //            CmdP.Parameters.AddWithValue("@ChemicalSN", strDelChemicalSN.Split(',')[i]);
        //            CmdP.ExecuteNonQuery();

        //            //Step 8：將「經濟部工廠食品前端比對統計資料(MOEAComFacStats)」重複性化學物質資料更新為正確的 ChemicalSN。
        //            CmdP.CommandText = "UPDATE MOEAComFacStats SET ChemicalSN=@NewChemicalSN";
        //            if (!String.IsNullOrWhiteSpace(dtChemicalData.Rows[i]["ChemiEngNameRev"].ToString()))
        //                CmdP.CommandText += ", ChemiEngNameRev=@ChemiEngNameRev";
        //            if (!String.IsNullOrWhiteSpace(dtChemicalData.Rows[i]["ChemiChnNameRev"].ToString()))
        //                CmdP.CommandText += ", ChemiChnNameRev=@ChemiChnNameRev";
        //            CmdP.CommandText += " WHERE ChemicalSN=@ChemicalSN";
        //            CmdP.Parameters.Clear();
        //            CmdP.Parameters.AddWithValue("@NewChemicalSN", strChemicalSN);
        //            if (!String.IsNullOrWhiteSpace(dtChemicalData.Rows[i]["ChemiEngNameRev"].ToString()))
        //                CmdP.Parameters.AddWithValue("@ChemiEngNameRev", dtChemicalData.Rows[i]["ChemiEngNameRev"].ToString());
        //            if (!String.IsNullOrWhiteSpace(dtChemicalData.Rows[i]["ChemiChnNameRev"].ToString()))
        //                CmdP.Parameters.AddWithValue("@ChemiChnNameRev", dtChemicalData.Rows[i]["ChemiChnNameRev"].ToString());
        //            CmdP.Parameters.AddWithValue("@ChemicalSN", strDelChemicalSN.Split(',')[i]);
        //            CmdP.ExecuteNonQuery();

        //            //Step Final：最後再移除「化學物質資料(ChemicalData)」重複性資料
        //            CmdP.CommandText = "DELETE FROM ChemicalData WHERE ChemicalSN=@ChemicalSN";
        //            CmdP.Parameters.Clear();
        //            CmdP.Parameters.AddWithValue("@ChemicalSN", strDelChemicalSN.Split(',')[i]);
        //            CmdP.ExecuteNonQuery();
        //        }

        //        //最後需要移除重複的化學物質資料再回傳此 DataTable。
        //        for (int i = dtChemicalData.Rows.Count - 1; i >= 0; i--)
        //        {
        //            for (int j = 0; j < strDelChemicalSN.Split(',').Length; j++)
        //            {
        //                //strDelChemicalSN.Split(',')[i]
        //                if (dtChemicalData.Rows[i]["ChemicalSN"].ToString() == strDelChemicalSN.Split(',')[j])
        //                {
        //                    dtChemicalData.Rows.RemoveAt(i);
        //                    continue;
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        funcUtil.ins2ndDataTransErrorLog("-", "2ndAP_funcMergeChemiData",
        //                                        "-", "化學物質更新失敗，" + ex.Message, "strChemicalEngName:" + strChemicalEngName +
        //                                        ";strChemicalChnName:" + strChemicalChnName + ";strCASNo:" + strCASNo);
        //    }
        //}

        /// <summary>
        /// 取得化學物質資料
        /// </summary>
        /// <param name="strChemicalEngName">化學物質英文名稱</param>
        /// <param name="strChemicalChnName">化學物質中文名稱</param>
        /// <param name="strCASNo">化學物質 CASNo</param>
        /// <param name="strTransId">交易編號DataId+yyMMddHHmmssfff</param>
        /// <param name="strPrimaryTableName">未合併前的來源資料表</param>
        /// <param name="strNo">記Temp DB XML的筆數(流水號)</param>
        /// <returns>化學物質欄位資料(DataTable)</returns>
        public static DataTable getChemicalData(string strChemicalEngName, string strChemicalChnName, string strCASNo,
                                                string strTransId, string strPrimaryTableName, string strNo)
        {
            string strChemiEngAliases = "";     //暫存化學物質英文別名
            string strChemiChnAliases = "";     //暫存化學物質中文別名
     
            dtChemicalData.Reset();     //初始化，每次呼叫本函式先清空回傳的暫存值

            if (strChemicalChnName == "-" && strChemicalEngName == "-" && strCASNo == "-")
                return dtChemicalData;
            if (strChemicalChnName == "" && strChemicalEngName == "" && strCASNo == "")
                return dtChemicalData;

            if (!(String.IsNullOrWhiteSpace(strChemicalEngName) && String.IsNullOrWhiteSpace(strChemicalChnName) && String.IsNullOrWhiteSpace(strCASNo)))
            {
                try
                {
                    SqlConnection ConnP = new SqlConnection(strPrimaryDBConn);
                    SqlCommand CmdP = ConnP.CreateCommand();
                    ConnP.Open();
                    CmdP.CommandTimeout = 3000;

                    CmdP.CommandText = "SELECT * FROM ChemicalData" +
                                       " WHERE ChemicalEngName=@ChemicalEngName AND ChemicalChnName=@ChemicalChnName AND CASNo=@CASNo";
                    CmdP.Parameters.Clear();
                    CmdP.Parameters.AddWithValue("@ChemicalEngName", strChemicalEngName);
                    CmdP.Parameters.AddWithValue("@ChemicalChnName", strChemicalChnName);
                    CmdP.Parameters.AddWithValue("@CASNo", strCASNo);
                    SqlDataAdapter drP = new SqlDataAdapter(CmdP);

                    drP.Fill(dtChemicalData);
                    DataTableReader SqlDR = new DataTableReader(new[] { dtChemicalData });
                    if (!SqlDR.HasRows)
                    {
                        //不存在於「化學物質資料(ChemicalData)」總表，需要新增該筆化學物質資料(只先塞中英文名稱及CASNo)。
                        CmdP.CommandText = "INSERT INTO ChemicalData" +
                                            " (ChemicalEngName, ChemicalChnName, CASNo, TransId, PrimaryTableName, No, ChemiEngAliases, ChemiChnAliases)" +
                                            " VALUES (@ChemicalEngName, @ChemicalChnName, @CASNo, @TransId, @PrimaryTableName, @No, @ChemiEngAliases, @ChemiChnAliases)";
                        CmdP.Parameters.Clear();
                        CmdP.Parameters.AddWithValue("@ChemicalEngName", strChemicalEngName);
                        CmdP.Parameters.AddWithValue("@ChemicalChnName", strChemicalChnName);
                        CmdP.Parameters.AddWithValue("@CASNo", strCASNo);
                        CmdP.Parameters.AddWithValue("@TransId", strTransId);
                        CmdP.Parameters.AddWithValue("@PrimaryTableName", strPrimaryTableName);
                        CmdP.Parameters.AddWithValue("@No", strNo);
                        CmdP.Parameters.AddWithValue("@ChemiEngAliases", strChemiEngAliases);
                        CmdP.Parameters.AddWithValue("@ChemiChnAliases", strChemiChnAliases);
                        CmdP.ExecuteNonQuery();

                        //插入完畢後取出該筆化學物質
                        CmdP.CommandText = "SELECT * FROM ChemicalData" +
                                       " WHERE ChemicalEngName=@ChemicalEngName AND ChemicalChnName=@ChemicalChnName AND CASNo=@CASNo";
                        CmdP.Parameters.Clear();
                        CmdP.Parameters.AddWithValue("@ChemicalEngName", strChemicalEngName);
                        CmdP.Parameters.AddWithValue("@ChemicalChnName", strChemicalChnName);
                        CmdP.Parameters.AddWithValue("@CASNo", strCASNo);
                        drP.Fill(dtChemicalData);
                    }

                    drP.Dispose();
                    ConnP.Dispose();
                    if (ConnP != null)
                        ConnP.Close();
                    CmdP.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    funcUtil.ins2ndDataTransErrorLog("-", "2ndAP_getChemicalSN",
                                                     "-", "新增化學物質失敗，" + ex.Message, "strChemicalEngName:" + strChemicalEngName +
                                                     ";strChemicalChnName:" + strChemicalChnName + ";strCASNo:" + strCASNo);
                }
            }

            return dtChemicalData;
        }

        public static DataTable getTPesticideSafetyChemicalData(string strChemicalEngName, string strChemicalChnName, string strCASNo,
                                                string strTransId, string strPrimaryTableName, string strNo)
        {
            string strChemiEngAliases = "";     //暫存化學物質英文別名
            string strChemiChnAliases = "";     //暫存化學物質中文別名

            dtChemicalData.Reset();     //初始化，每次呼叫本函式先清空回傳的暫存值

            if (strChemicalChnName == "-" && strChemicalEngName == "-" && strCASNo == "-")
                return dtChemicalData;
            if (strChemicalChnName == "" && strChemicalEngName == "" && strCASNo == "")
                return dtChemicalData;

            if (!(String.IsNullOrWhiteSpace(strChemicalEngName) && String.IsNullOrWhiteSpace(strChemicalChnName) && String.IsNullOrWhiteSpace(strCASNo)))
            {
                try
                {
                    SqlConnection ConnP = new SqlConnection(strPrimaryDBConn);
                    SqlCommand CmdP = ConnP.CreateCommand();
                    ConnP.Open();
                    CmdP.CommandTimeout = 3000;

                    CmdP.CommandText = "SELECT * FROM ChemicalData" +
                                       " WHERE ChemicalEngName=@ChemicalEngName AND ChemicalChnName=@ChemicalChnName AND CASNo=@CASNo AND PrimaryTableName=@PrimaryTableName";
                    CmdP.Parameters.Clear();
                    CmdP.Parameters.AddWithValue("@ChemicalEngName", strChemicalEngName);
                    CmdP.Parameters.AddWithValue("@ChemicalChnName", strChemicalChnName);
                    CmdP.Parameters.AddWithValue("@CASNo", strCASNo);
                    CmdP.Parameters.AddWithValue("@PrimaryTableName", strPrimaryTableName);
                    SqlDataAdapter drP = new SqlDataAdapter(CmdP);

                    drP.Fill(dtChemicalData);
                    DataTableReader SqlDR = new DataTableReader(new[] { dtChemicalData });
                    if (!SqlDR.HasRows)
                    {
                        //不存在於「化學物質資料(ChemicalData)」總表，需要新增該筆化學物質資料(只先塞中英文名稱及CASNo)。
                        CmdP.CommandText = "INSERT INTO ChemicalData" +
                                            " (ChemicalEngName, ChemicalChnName, CASNo, TransId, PrimaryTableName, No, ChemiEngAliases, ChemiChnAliases)" +
                                            " VALUES (@ChemicalEngName, @ChemicalChnName, @CASNo, @TransId, @PrimaryTableName, @No, @ChemiEngAliases, @ChemiChnAliases)";
                        CmdP.Parameters.Clear();
                        CmdP.Parameters.AddWithValue("@ChemicalEngName", strChemicalEngName);
                        CmdP.Parameters.AddWithValue("@ChemicalChnName", strChemicalChnName);
                        CmdP.Parameters.AddWithValue("@CASNo", strCASNo);
                        CmdP.Parameters.AddWithValue("@TransId", strTransId);
                        CmdP.Parameters.AddWithValue("@PrimaryTableName", strPrimaryTableName);
                        CmdP.Parameters.AddWithValue("@No", strNo);
                        CmdP.Parameters.AddWithValue("@ChemiEngAliases", strChemiEngAliases);
                        CmdP.Parameters.AddWithValue("@ChemiChnAliases", strChemiChnAliases);
                        CmdP.ExecuteNonQuery();

                        //插入完畢後取出該筆化學物質
                        CmdP.CommandText = "SELECT * FROM ChemicalData" +
                                       " WHERE ChemicalEngName=@ChemicalEngName AND ChemicalChnName=@ChemicalChnName AND CASNo=@CASNo";
                        CmdP.Parameters.Clear();
                        CmdP.Parameters.AddWithValue("@ChemicalEngName", strChemicalEngName);
                        CmdP.Parameters.AddWithValue("@ChemicalChnName", strChemicalChnName);
                        CmdP.Parameters.AddWithValue("@CASNo", strCASNo);
                        drP.Fill(dtChemicalData);
                    }

                    drP.Dispose();
                    ConnP.Dispose();
                    if (ConnP != null)
                        ConnP.Close();
                    CmdP.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    funcUtil.ins2ndDataTransErrorLog("-", "2ndAP_getChemicalSN",
                                                     "-", "新增化學物質失敗，" + ex.Message, "strChemicalEngName:" + strChemicalEngName +
                                                     ";strChemicalChnName:" + strChemicalChnName + ";strCASNo:" + strCASNo);
                }
            }

            return dtChemicalData;
        }
    }
}