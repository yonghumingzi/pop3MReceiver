using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace pop3MReceiver
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            button2.Enabled = false;
            button3.Enabled = false;
            button4.Enabled = false;
        }

        TcpClient server;
        NetworkStream NetStream;
        StreamReader RdStream;
        string Data;
        byte[] bytesData;
        private const string LINE_END = "\r\n";

        private bool getInput(string[] input)
        {
            string host = textBox1.Text;  //邮件服务器(POP3)
            string mail = textBox2.Text;  //邮箱账号
            string pass = textBox3.Text;  //密码
            if(host == "" || mail == "" || pass == "")
            {
                MessageBox.Show("服务器、邮箱和密码都必须填写！");
                return false;
            }
            else
            {
                input[0] = host;
                input[1] = mail;
                input[2] = pass;
                return true;
            }
        }

        //登录
        private void Button1_Click(object sender, EventArgs e)
        {
            listView1.Items.Clear();
            string[] input = new string[3];
            if (getInput(input))
            {
                string host = input[0];
                int port = 110;  //pop3 110
                server = new TcpClient(host, port);
                string stringTmp = "";
                try
                {
                    NetStream = server.GetStream();
                    RdStream = new StreamReader(NetStream, Encoding.Default);
                    listBox1.Items.Add(RdStream.ReadLine());
                    
                    // USER命令
                    Data = "USER " + input[1] + LINE_END;
                    listBox1.Items.Add(Data);
                    bytesData = System.Text.Encoding.ASCII.GetBytes(Data.ToCharArray());                    
                    NetStream.Write(bytesData, 0, bytesData.Length);
                    listBox1.Items.Add(RdStream.ReadLine());
                    
                    // PASS命令
                    Data = "PASS " + input[2] + LINE_END;
                    listBox1.Items.Add("PASS sent");
                    bytesData = Encoding.ASCII.GetBytes(Data.ToCharArray());
                    NetStream.Write(bytesData, 0, bytesData.Length);
                    stringTmp = RdStream.ReadLine();
                    if(stringTmp == "-ERR ERR.LOGIN.PASSERR")
                    {
                        MessageBox.Show("用户名或密码错误");
                        listBox1.Items.Add("-ERR ERR.LOGIN.PASSERR");
                        return;
                    }
                    else
                    {
                        button1.Enabled = false;
                        button2.Enabled = true;
                        button3.Enabled = true;
                        button4.Enabled = true;
                        listBox1.Items.Add(stringTmp);
                    }
                    //STAT命令，用于显示邮件总数和总大小情况
                    Data = "STAT" + LINE_END;
                    listBox1.Items.Add(Data);
                    bytesData = System.Text.Encoding.ASCII.GetBytes(Data.ToCharArray());
                    NetStream.Write(bytesData, 0, bytesData.Length);
                    stringTmp = RdStream.ReadLine();
                    listBox1.Items.Add("共" + stringTmp.Split(' ')[1] + "封邮件，共" + stringTmp.Split(' ')[2] + "字节");
                 }
                catch (InvalidOperationException err)
                {
                    MessageBox.Show("用户名或密码错误");
                    listBox1.Items.Add("Error: " + err.ToString());
                }
            }
        }

        // 获取邮件
        private void Button2_Click(object sender, EventArgs e)
        {
            string dataTemp;
            string[] arrTemp;
            string boundary = "";
            string cont = "";
            string[] arrRet = new string[10];
            int numberofMails = 0;   // 邮件总数 

            try
            {
                //STAT命令
                Data = "STAT" + LINE_END;
                listBox1.Items.Add(Data);
                bytesData = System.Text.Encoding.ASCII.GetBytes(Data.ToCharArray());
                NetStream.Write(bytesData, 0, bytesData.Length);
                dataTemp = RdStream.ReadLine();
                numberofMails = Convert.ToInt32(dataTemp.Split(' ')[1]);
                listBox1.Items.Add("共" + dataTemp.Split(' ')[1] + "封邮件，共" + dataTemp.Split(' ')[2] + "字节");

                // 超过邮件范围
                if(numericUpDown1.Value > numberofMails)
                {
                    MessageBox.Show("不存在第" + numericUpDown1.Value.ToString() + "封邮件，请重新选择。");
                    return;
                }

                // LIST命令
                Data = "LIST " + numericUpDown1.Value.ToString() + LINE_END;
                bytesData = System.Text.Encoding.ASCII.GetBytes(Data.ToCharArray());
                NetStream.Write(bytesData, 0, bytesData.Length);
                dataTemp = RdStream.ReadLine();
                arrRet[5] = dataTemp.Split(' ')[2];

                // RETR命令
                Data = "RETR " + numericUpDown1.Value.ToString() + LINE_END;
                bytesData = System.Text.Encoding.ASCII.GetBytes(Data.ToCharArray());
                NetStream.Write(bytesData, 0, bytesData.Length);
                dataTemp = RdStream.ReadLine();
                if(dataTemp == "-ERR invalid message")
                {
                    MessageBox.Show("获取失败，请重试。");
                    return;
                }
                listBox1.Items.Add(dataTemp);

                while(dataTemp != ".")
                {
                    // 提取boundary
                    if (boundary == "" && dataTemp != "" && dataTemp.Substring(0,10) == "\tboundary=")
                    {
                        boundary = dataTemp.Substring(11);
                        boundary = "--" + boundary.Substring(0, boundary.Length - 1);
                        continue;
                    }

                    // 获取内容
                    if (boundary != "" && dataTemp == boundary)
                    {
                        for(int i = 0; i < 4; i++) dataTemp = RdStream.ReadLine();
                        
                        while(dataTemp != boundary)
                        {
                            cont += dataTemp;
                            dataTemp = RdStream.ReadLine();
                        }

                        byte[] bytecont = Convert.FromBase64String(cont);
                        arrRet[4] = parseContentOfMail(Encoding.GetEncoding("gb18030").GetString(bytecont));
                        //if(arrRet[5] == "")
                        //{
                        //    MessageBox.Show("邮件内容解析失败");
                        //}
                    }

                    arrTemp = dataTemp.Split(":".ToCharArray());
                    
                    switch (arrTemp[0])
                    {
                            case "Date":
                                arrRet[0] = arrTemp[1];
                                break;
                            case "From":
                                arrRet[1] = arrTemp[1];
                                break;
                            case "To":
                                arrRet[2] = arrTemp[1];
                                break;
                            case "Subject":
                                arrRet[3] = arrTemp[1];
                                break;
                            default:
                                break;
                    }

                    dataTemp = RdStream.ReadLine();                       
                 }
                    
                textBox4.Text = "时间: " + arrRet[0] + LINE_END;
                textBox4.Text += "发送者: " + arrRet[1] + LINE_END;
                textBox4.Text += "接收者: " + arrRet[2] + LINE_END;
                textBox4.Text += "大小: " + arrRet[5] + "字节" +LINE_END;

                // 对邮件内容进行base64解码
                string subjectStr = "";
                try
                {
                    subjectStr = Base64GbkDecode(arrRet[3].Split('?')[3]);  // 中文GBK编码
                }
                catch(IndexOutOfRangeException err)
                {
                    subjectStr = arrRet[3];    // 英文
                }

                textBox4.Text += "主题: " + subjectStr + LINE_END;
                System.Text.Encoding.GetEncoding("GB18030");
                textBox4.Text += "内容: " + LINE_END + arrRet[4].Replace("\n", LINE_END) + LINE_END;
                ListViewItem item = listView1.Items.Add(arrRet[1]);            
                item.SubItems.Add(subjectStr);
                item.SubItems.Add(arrRet[0]);
            }
            catch(InvalidOperationException err)
            {
                MessageBox.Show("错误: " + err.ToString());
                listBox1.Items.Add("Error: " + err.ToString());
            }
        }

        // 清空
        private void Button3_Click(object sender, EventArgs e)
        {
            textBox3.Clear();
            textBox4.Clear();
            listView1.Clear();
            listBox1.Items.Clear();
            
            // QUIT命令
            Data = "QUIT " + LINE_END;
            listBox1.Items.Add(Data);
            bytesData = System.Text.Encoding.ASCII.GetBytes(Data.ToCharArray());
            NetStream.Write(bytesData, 0, bytesData.Length);
            listBox1.Items.Add(RdStream.ReadLine());

            NetStream.Close();
            RdStream.Close();

            button1.Enabled = true;
            button2.Enabled = false;
            button3.Enabled = false;
            button4.Enabled = false;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            int numberofMails;
            string dataTemp;

            //STAT命令，获取邮件总数
            Data = "STAT" + LINE_END;
            listBox1.Items.Add(Data);
            bytesData = System.Text.Encoding.ASCII.GetBytes(Data.ToCharArray());
            NetStream.Write(bytesData, 0, bytesData.Length);
            dataTemp = RdStream.ReadLine();
            numberofMails = Convert.ToInt32(dataTemp.Split(' ')[1]);
            listBox1.Items.Add("当前邮件总数: " + Convert.ToInt32(numberofMails));

            if (numberofMails > 0)
            {
                for (int i = 1; i <= numberofMails; i++)
                {
                    // LIST命令
                    Data = "LIST " + Convert.ToInt32(i) + LINE_END;
                    bytesData = System.Text.Encoding.ASCII.GetBytes(Data.ToCharArray());
                    NetStream.Write(bytesData, 0, bytesData.Length);
                    dataTemp = RdStream.ReadLine();
                    listBox1.Items.Add("  第"+ Convert.ToInt32(i) + "封 大小: " + dataTemp.Split(' ')[2] + "字节");
                }
            }
            else
            {
                listBox1.Items.Add("Empty.");
            }
        }

        //base64解码
        public static string Base64Utf8Decode(string data)
        {
            string result = "";
            try
            {
                System.Text.UTF8Encoding encoder = new System.Text.UTF8Encoding();
                System.Text.Decoder utf8Decode = encoder.GetDecoder();
                byte[] todecode_byte = Convert.FromBase64String(data);
                int charCount = utf8Decode.GetCharCount(todecode_byte, 0, todecode_byte.Length);
                char[] decoded_char = new char[charCount];
                utf8Decode.GetChars(todecode_byte, 0, todecode_byte.Length, decoded_char, 0);
                result = new String(decoded_char);
            }
            catch (Exception e)
            {
                return "Error in base64Encode" + e.Message;
            }
            return result;
        }

        public static string Base64GbkDecode(string data)
        {
            string decode = "";
            byte[] bytes = Convert.FromBase64String(data);
            try
            {
                decode = Encoding.GetEncoding("gb2312").GetString(bytes);
            }
            catch (Exception ex1)
            {
                return "Error in base64Encode" + ex1.Message;
            }
            return decode;
        }

        // 解析特定格式的邮件内容
        public static string parseContentOfMail(string data)
        {
            Regex regex = new Regex(@"(?<content>[\s\S]*?)\n\n\n| |\n([\s\S]*?)\n|\n|\n邮箱：([\s\S]*?)\n|\n\n([\s\S]*?)", RegexOptions.IgnoreCase);
            Match match = regex.Match(data);
            if (match.Success)
            {
                return match.Groups["content"].Value;
            }
            else
            {
                return "";
            }
        }
    }
}

        
