using MessagingToolkit.Barcode;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ReadBarCode
{
    public partial class Form1 : Form
    {
        IDatabase db = SeRedis.redis.GetDatabase();
        string direc_pdf = ConfigurationManager.AppSettings["filedir"];
        string direc_img = ConfigurationManager.AppSettings["ImagePath"];
        int count = Convert.ToInt32(ConfigurationManager.AppSettings["count"].ToString());
        public Form1()
        {
            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if(this.button1.Text.Trim()=="开 始 识 别")
            {
                this.button1.Text = "识 别 中...";
                System.Threading.Thread thread = new System.Threading.Thread(runTask);
                thread.IsBackground = true;
                thread.Start();
                this.button1.Enabled = true;
            }
            else if (this.button1.Text.Trim() == "识 别 中...")
            {
                this.button1.Text = "正 在 停 止";
                this.button1.Enabled = false;
            }
            
        }
       

        private void runTask()
        {
            if (ConfigurationManager.AppSettings["AutoRun"].ToString().Trim() == "Y")
            {
                fn_share fn_share = new fn_share();
                string filedic = System.Environment.CurrentDirectory + @"\log\";
                if (!Directory.Exists(filedic))
                {
                    Directory.CreateDirectory(filedic);
                }
                while(true)
                {
                    if (this.button1.Text.Trim() == "正 在 停 止")
                    {
                        break;
                    }
                    System.Threading.Thread.Sleep(3000);
                    string filename = filedic + "barcode_log_" + DateTime.Now.ToString("yyyyMMddHH") + ".txt";
                    for (int i = 0; i < count; i++)
                    {
                        //barcode(fn_share, filename);
                        barcode_toolKit(fn_share, filename);
                    }
                }
                this.button1.Text = "开 始 识 别";
                this.button1.Enabled = true;
            }
        }
        public enum Definition
        {
            One = 1, Two = 2, Three = 3, Four = 4, Five = 5, Six = 6, Seven = 7, Eight = 8, Nine = 9, Ten = 10
        }
        /// <summary>
        /// zbar识别
        /// </summary>
        /// <param name="fn_share"></param>
        /// <param name="filename"></param>
        private void barcode(fn_share fn_share, string filename)
        {

            string json = string.Empty; string sql = string.Empty;
            string guid = string.Empty; string decoded = string.Empty; string barcode = string.Empty;

            if (db.KeyExists("recognizetask"))
            {
                JObject jo = null;
                try
                {
                    json = db.ListLeftPop("recognizetask");
                    fn_share.systemLog(filename, "-------------------------------------------------------------\r\n");
                    if (!string.IsNullOrEmpty(json))
                    {
                        jo = (JObject)JsonConvert.DeserializeObject(json);
                        //只有PDF文件才会进行条形码识别
                        sql = @"select t.* from list_attachment t where t.ordercode='" + jo.Value<string>("ordercode") + "' and upper(t.filesuffix)='PDF'";
                        DataTable dt = DBMgr.GetDataTable(sql);
                        if (dt.Rows.Count > 0)
                        {
                            DateTime d1 = DateTime.Now;
                            guid = Guid.NewGuid().ToString();
                            //ConvertPDF2Image(direc_pdf + dt.Rows[0]["FILENAME"], direc_img, guid, 1, 1, ImageFormat.Jpeg, Definition.Ten);
                            ConvertPDF.pdfToPic(direc_pdf + dt.Rows[0]["FILENAME"], direc_img, guid, 1, 1, ImageFormat.Jpeg);
                            string fileName = direc_img + guid + ".Jpeg";
                            fn_share.systemLog(filename, "=== ConvertToImage——" + (DateTime.Now - d1) + "\r\n");
                            if (File.Exists(fileName))
                            {
                                Image primaryImage = Image.FromFile(fileName);
                                Bitmap pImg = MakeGrayscale3((Bitmap)primaryImage);
                                
                                using (ZBar.ImageScanner scanner = new ZBar.ImageScanner())
                                {
                                    scanner.SetConfiguration(ZBar.SymbolType.None, ZBar.Config.Enable, 0);
                                    scanner.SetConfiguration(ZBar.SymbolType.QRCODE, ZBar.Config.Enable, 1);
                                    scanner.SetConfiguration(ZBar.SymbolType.CODE128, ZBar.Config.Enable, 1);
                                    List<ZBar.Symbol> symbols = new List<ZBar.Symbol>();
                                    symbols = scanner.Scan((Image)pImg);
                                    if (symbols != null && symbols.Count > 0)
                                    {
                                        decoded = symbols[0].Data;
                                    }
                                    if (!string.IsNullOrEmpty(decoded))//有些PDF文件并无条形码
                                    {
                                        //barcode = barconvert(decoded);//编码转换 (这是28系统的转换)
                                        barcode = decoded;
                                    }
                                    else
                                    {
                                        barcode = "001";
                                    }
                                    sql = "update list_order set cusno='" + barcode + "' where code='" + jo.Value<string>("ordercode") + "'";
                                    DBMgr.ExecuteNonQuery(sql);
                                }
                            }
                            fn_share.systemLog(filename, "=== " + jo.Value<string>("ordercode") + "——" + (DateTime.Now - d1) + "——" + barcode + "\r\n");
                            
                        }
                    }

                }
                catch (Exception ex)
                {
                    //db.ListRightPush("recognizetask", json);
                    sql = "update list_order set cusno='002' where code='" + jo.Value<string>("ordercode") + "'";
                    DBMgr.ExecuteNonQuery(sql);
                    fn_share.systemLog(filename, jo.Value<string>("ordercode")+ "  异常，识别条码失败：" + ex.Message + "\r\n");
                }
            }
        }
        private void barcode_toolKit(fn_share fn_share, string filename)
        {

            string json = string.Empty; string sql = string.Empty;
            string guid = string.Empty; string decoded = string.Empty; string barcode = string.Empty;

            if (db.KeyExists("recognizetask"))
            {
                JObject jo = null;
                try
                {
                    json = db.ListLeftPop("recognizetask");
                    fn_share.systemLog(filename, "-------------------------------------------------------------\r\n");
                    if (!string.IsNullOrEmpty(json))
                    {
                        jo = (JObject)JsonConvert.DeserializeObject(json);
                        //只有PDF文件才会进行条形码识别
                        sql = @"select t.* from list_attachment t where t.ordercode='" + jo.Value<string>("ordercode") + "' and upper(t.filesuffix)='PDF'";
                        DataTable dt = DBMgr.GetDataTable(sql);
                        if (dt.Rows.Count > 0)
                        {
                            DateTime d1 = DateTime.Now;
                            guid = Guid.NewGuid().ToString();
                            ConvertPDF.pdfToPic(direc_pdf + dt.Rows[0]["FILENAME"], direc_img, guid, 1, 1, ImageFormat.Jpeg);//pdf转图片
                            string fileName = direc_img + guid + ".Jpeg";
                            fn_share.systemLog(filename, "=== ConvertToImage——" + (DateTime.Now - d1) + "\r\n");
                            if (File.Exists(fileName))
                            {
                                BarcodeDecoder barcodeDecoder = new BarcodeDecoder();
                                Image primaryImage = Image.FromFile(fileName);
                                Bitmap pImg = MakeGrayscale3((Bitmap)primaryImage);
                                Dictionary<DecodeOptions, object> decodingOptions = new Dictionary<DecodeOptions, object>();
                                List<BarcodeFormat> possibleFormats = new List<BarcodeFormat>(10);
                                possibleFormats.Add(BarcodeFormat.Code128);
                                possibleFormats.Add(BarcodeFormat.EAN13);
                                decodingOptions.Add(DecodeOptions.TryHarder, true);
                                decodingOptions.Add(DecodeOptions.PossibleFormats, possibleFormats);
                                DateTime d2 = DateTime.Now;
                                Result decodedResult = barcodeDecoder.Decode(pImg, decodingOptions);
                                Console.WriteLine("解析时长:" + (DateTime.Now - d2));
                                //while (decodedResult == null)
                                //{
                                //    System.Threading.Thread.Sleep(500);
                                //}
                                if (decodedResult != null)//有些PDF文件并无条形码
                                {
                                    barcode = decodedResult.Text;
                                }
                                else
                                {
                                    barcode = "001";
                                }
                                sql = "update list_order set cusno='" + barcode + "' where code='" + jo.Value<string>("ordercode") + "'";
                                DBMgr.ExecuteNonQuery(sql);

                                
                            }
                            fn_share.systemLog(filename, "=== " + jo.Value<string>("ordercode") + "——" + (DateTime.Now - d1) + "——" + barcode + "\r\n");

                        }
                    }

                }
                catch (Exception ex)
                {
                    //db.ListRightPush("recognizetask", json);
                    sql = "update list_order set cusno='002' where code='" + jo.Value<string>("ordercode") + "'";
                    DBMgr.ExecuteNonQuery(sql);
                    fn_share.systemLog(filename, jo.Value<string>("ordercode") + "  异常，识别条码失败：" + ex.Message + "\r\n");
                }
            }
        }
        /// <summary>
        /// 处理图片灰度
        /// </summary>
        /// <param name="original"></param>
        /// <returns></returns>
        public static Bitmap MakeGrayscale3(Bitmap original)
        {
            //截取文件右上方1/4部分处理、识别
            //Rectangle cloneRect = new Rectangle(original.Width / 2, 0, original.Width / 2, original.Height / 2);
            //Bitmap newBitmap = original.Clone(cloneRect, original.PixelFormat);
            //create a blank bitmap the same size as original
            Bitmap newBitmap = new Bitmap(original.Width, original.Height);
            //newBitmap.Save(@"E:\test\44\2017-05-25\bit.jpg");
            //get a graphics object from the new image
            Graphics g = Graphics.FromImage(newBitmap);

            //create the grayscale ColorMatrix
            System.Drawing.Imaging.ColorMatrix colorMatrix = new System.Drawing.Imaging.ColorMatrix(
               new float[][] 
              {
                 new float[] {.3f, .3f, .3f, 0, 0},
                 new float[] {.59f, .59f, .59f, 0, 0},
                 new float[] {.11f, .11f, .11f, 0, 0},
                 new float[] {0, 0, 0, 1, 0},
                 new float[] {0, 0, 0, 0, 1}
              });

            //create some image attributes
            ImageAttributes attributes = new ImageAttributes();

            //set the color matrix attribute
            attributes.SetColorMatrix(colorMatrix);

            //draw the original image on the new image
            //using the grayscale color matrix
            g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height),
               0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);

            //dispose the Graphics object
            g.Dispose();
            return newBitmap;
        }

        /// <summary>
        /// 生成小图片
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <param name="Width"></param>
        /// <param name="Height"></param>
        /// <param name="image"></param>
        /// <param name="name"></param>
        private static void CutPicture(int X, int Y, int Width, int Height, Image image, string path)
        {
            if (image.Width < X + Width || image.Height < Y + Height)
            {
                MessageBox.Show("截取的区域超过了图片本身的高度、宽度", "错误！", MessageBoxButtons.OK, MessageBoxIcon.Error);

            }
            Bitmap Bmp = new Bitmap(image);
            Rectangle cloneRect = new Rectangle(X, Y, Width, Height);
            Bitmap cloneBmp = Bmp.Clone(cloneRect, Bmp.PixelFormat);
            cloneBmp.Save(path);
        }

        private void button2_Click(object sender, EventArgs e)
        {

        }
    }
}
