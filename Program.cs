using MessagingToolkit.Barcode;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using O2S.Components.PDFRender4NET;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;

namespace ReadBarCode
{
    class Program
    {
        IDatabase db = SeRedis.redis.GetDatabase();
        string direc_pdf = ConfigurationManager.AppSettings["filedir"];
        string direc_img = ConfigurationManager.AppSettings["ImagePath"];        

        static void Main(string[] args)
        {
            if (ConfigurationManager.AppSettings["AutoRun"].ToString().Trim() == "Y")
            {
                fn_share fn_share = new fn_share();
                string filedic = System.Environment.CurrentDirectory + @"\log\";
                if (!Directory.Exists(filedic))
                {
                    Directory.CreateDirectory(filedic);
                }
                string filename = filedic + "barcode_log_" + DateTime.Now.ToString("yyyyMMddHH") + ".txt";

                Program p = new Program();

                fn_share.systemLog(filename, "----------------------------------------------------------------------------------------------------\r\n\r\n");

                int count = Convert.ToInt32(ConfigurationManager.AppSettings["count"].ToString());
                for (int i = 0; i < count; i++)
                {
                    fn_share.systemLog(filename, "================ " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " start================ \r\n");

                    p.barcode(fn_share, filename);

                    fn_share.systemLog(filename, "================ " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " end  ================ \r\n");
                }
            }
        }
        public enum Definition
        {
            One = 1, Two = 2, Three = 3, Four = 4, Five = 5, Six = 6, Seven = 7, Eight = 8, Nine = 9, Ten = 10
        }
        private void ConvertPDF2Image(string pdfInputPath, string imageOutputPath, string imageName, int startPageNum, int endPageNum, ImageFormat imageFormat, Definition definition)
        {
            PDFFile pdfFile = PDFFile.Open(pdfInputPath);
            if (!Directory.Exists(imageOutputPath))
            {
                Directory.CreateDirectory(imageOutputPath);
            }
            for (int i = startPageNum; i <= endPageNum; i++)
            {
                Bitmap pageImage = pdfFile.GetPageImage(i - 1, 56 * (int)definition);
                pageImage.Save(imageOutputPath + imageName + "." + imageFormat.ToString(), imageFormat);
                pageImage.Dispose();
            }
            pdfFile.Dispose();
        }

        private void barcode(fn_share fn_share, string filename)
        {
           
            string json = string.Empty; string sql = string.Empty;
            string guid = string.Empty; string decoded = string.Empty; string barcode = string.Empty;

            if (db.KeyExists("recognizetask"))
            {
                try
                {                     

                    json = db.ListLeftPop("recognizetask");
                    fn_share.systemLog(filename, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " json：" + json + "\r\n");

                    if (!string.IsNullOrEmpty(json))
                    {
                        JObject jo = (JObject)JsonConvert.DeserializeObject(json);
                        //只有PDF文件才会进行条形码识别
                        sql = @"select t.* from list_attachment t where t.ordercode='" + jo.Value<string>("ordercode") + "' and upper(t.filesuffix)='PDF'";
                        DataTable dt = DBMgr.GetDataTable(sql);
                        if (dt.Rows.Count > 0)
                        {
                            guid = Guid.NewGuid().ToString();
                            ConvertPDF2Image(direc_pdf + dt.Rows[0]["FILENAME"], direc_img, guid, 1, 1, ImageFormat.Jpeg, Definition.Ten);
                            BarcodeDecoder barcodeDecoder = new BarcodeDecoder();
                            if (File.Exists(direc_img + guid + ".Jpeg"))
                            {
                                System.Drawing.Bitmap image = new System.Drawing.Bitmap(direc_img + guid + ".Jpeg");
                                Dictionary<DecodeOptions, object> decodingOptions = new Dictionary<DecodeOptions, object>();
                                List<BarcodeFormat> possibleFormats = new List<BarcodeFormat>(10);
                                possibleFormats.Add(BarcodeFormat.Code128);
                                possibleFormats.Add(BarcodeFormat.EAN13);
                                decodingOptions.Add(DecodeOptions.TryHarder, true);
                                decodingOptions.Add(DecodeOptions.PossibleFormats, possibleFormats);
                                Result decodedResult = barcodeDecoder.Decode(image, decodingOptions);
                                if (decodedResult != null)//有些PDF文件并无条形码
                                {
                                    decoded = decodedResult.Text;
                                    fn_share.systemLog(filename, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " decoded：" + decoded + "\r\n");
                                    barcode = barconvert(decoded);//编码转换 
                                    fn_share.systemLog(filename, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " barcode：" + barcode + "\r\n");

                                    sql = "update list_order set cusno='" + barcode + "' where code='" + jo.Value<string>("ordercode") + "'";
                                    DBMgr.ExecuteNonQuery(sql);
                                    fn_share.systemLog(filename, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  list_order.code：" + jo.Value<string>("ordercode") + "   更新sql结束 \r\n");
                                }
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    db.ListRightPush("recognizetask", json);
                    fn_share.systemLog(filename, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  异常，识别条码失败：" + ex.Message + "\r\n");
                }
            }
        }

        private string barconvert(string barcode)
        {
            string json_convert = @"{'01':'AIKSQW','02':'AIKSQN','03':'SIKS','04':'SEKSA','05':'SEKSB','06':'SEKSC','07':'SEKSD',
                                     '08':'SEKSE','09':'SEKSF','10':'SEKSG','11':'SEKSH','12':'SEKSI','13':'SEKSJ','14':'SEKSK',
                                     '15':'SEKSL','16':'SEKSM','17':'SEKSN','18':'SEKSO','19':'SEKSP','20':'SEKSQ','21':'SEKSR',
                                     '22':'SEKSS','23':'SEKST','24':'SEKSU','25':'SEKSV','26':'SEKSW','27':'SEKSX','28':'SEKSY',
                                     '29':'SEKSZ','30':'ILY','31':'ELY','35':'AEKS','45':'DJRIKS','46':'DJREKS','47':'DJCIKS',
                                     '48':'DJCEKS','49':'JGIKS','50':'JGEKS','51':'GJIKS','52':'GJEKS'}";
            JObject jo_convert = (JObject)JsonConvert.DeserializeObject(json_convert);
            string prefix = barcode.Substring(0, 2);
            if (!string.IsNullOrEmpty(jo_convert.Value<string>(prefix)))
            {
                barcode = jo_convert.Value<string>(prefix) + barcode.Substring(2);
            }
            return barcode;
        }

    }
}
