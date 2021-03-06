﻿using System;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.IO;
using Microsoft.Win32;
using System.Security.Principal;
using System.ComponentModel;

using System.Deployment.Application;
using System.Reflection;


namespace Locker
{
    public partial class LockerForm : Form
    {
        // Encryption
        CspParameters cspp = new CspParameters();
        RSACryptoServiceProvider rsa;

        // Key
        const string keyName = "LockerKey";

        // Features
        const string rightClickText = "Encrypt/Decrypt Folder";
        string SelectedFileLocation = null;
        Boolean rightClickState = false;

        // Thread
        private BackgroundWorker backgroundEncrypter;
        private BackgroundWorker backgroundDecrypter;
        private int highestPercentageReached = 0;


        public LockerForm()
        {
            InitializeComponent();
            InitializeBackgroundWorker(); 
        }

        public void InitializeBackgroundWorker()
        {
            // Encrypter
            backgroundEncrypter.DoWork += new DoWorkEventHandler(background_DoEncrypt);
            backgroundEncrypter.RunWorkerCompleted += new RunWorkerCompletedEventHandler(backgroundWorker_RunWorkerCompleted);
            backgroundEncrypter.ProgressChanged += new ProgressChangedEventHandler(backgroundWorker_ProgressChanged);

            // Decrypter
            backgroundDecrypter.DoWork += new DoWorkEventHandler(background_DoDecrypt);
            backgroundDecrypter.RunWorkerCompleted += new RunWorkerCompletedEventHandler(backgroundWorker_RunWorkerCompleted);
            backgroundDecrypter.ProgressChanged += new ProgressChangedEventHandler(backgroundWorker_ProgressChanged);
        }

        private void background_DoEncrypt(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            e.Result = EncryptFile((FileInfo)e.Argument, worker, e);
        }

        private void background_DoDecrypt(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            e.Result = DecryptFile((FileInfo)e.Argument, worker, e);
        }

        private void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                UpdateStatus(e.Error.Message);
            }
            else if (e.Cancelled)
            {
                UpdateStatus("Canceled");
            }
            else
            {
                UpdateStatus(e.Result.ToString());
            }
            EnableButtons(true);
        }

        private void backgroundWorker_ProgressChanged(object sender,ProgressChangedEventArgs e)
        {
            this.progressBar1.Value = e.ProgressPercentage;
        }

        private void LockerForm_Load(object sender, EventArgs e)
        {
            version.Text = CurrentVersion;
            RegistryKey _key = Registry.ClassesRoot.OpenSubKey($@"Unknown\shell\{rightClickText}", false);
            rightClickState = _key == null ? false : true;
            rightClickOptionMenuItem.Checked = rightClickState;
            string[] args = Environment.GetCommandLineArgs();
            try
            {
                if (args.Length > 1 && File.Exists(args[1]))
                {
                    SelectedFileLocation = args[1];
                    richTextBox.Text = "Selected File " + SelectedFileLocation;
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show("Unable to Load the file\n\n"+ex.Message,"Error",MessageBoxButtons.OK);
            }
        }

        private string EncryptFile(FileInfo fInfo, BackgroundWorker worker, DoWorkEventArgs e)
        {
            string inFile = fInfo.FullName;
            // Create instance of Rijndael for
            // symetric encryption of the data.
            RijndaelManaged rjndl = new RijndaelManaged();
            rjndl.KeySize = 256;
            rjndl.BlockSize = 256;
            rjndl.Mode = CipherMode.CBC;
            ICryptoTransform transform = rjndl.CreateEncryptor();

            // Use RSACryptoServiceProvider to
            // enrypt the Rijndael key.
            // rsa is previously instantiated: 
            //    rsa = new RSACryptoServiceProvider(cspp);
            byte[] keyEncrypted = rsa.Encrypt(rjndl.Key, false);

            // Create byte arrays to contain
            // the length values of the key and IV.
            byte[] LenK = new byte[4];
            byte[] LenIV = new byte[4];

            int lKey = keyEncrypted.Length;
            LenK = BitConverter.GetBytes(lKey);
            int lIV = rjndl.IV.Length;
            LenIV = BitConverter.GetBytes(lIV);

            // Write the following to the FileStream
            // for the encrypted file (outFs):
            // - length of the key
            // - length of the IV
            // - ecrypted key
            // - the IV
            // - the encrypted cipher content

            int startFileName = inFile.LastIndexOf("\\") + 1;
            // Change the file's extension to "_enc"

            string outFile = fInfo.Directory.FullName +"\\" +inFile.Substring(startFileName, inFile.LastIndexOf(".") - startFileName) +fInfo.Extension+"_enc";

            using (FileStream outFs = new FileStream(outFile, FileMode.Create))
            {
                outFs.Write(LenK, 0, 4);
                outFs.Write(LenIV, 0, 4);
                outFs.Write(keyEncrypted, 0, lKey);
                outFs.Write(rjndl.IV, 0, lIV);

                // Now write the cipher text using
                // a CryptoStream for encrypting.
                using (CryptoStream outStreamEncrypted = new CryptoStream(outFs, transform, CryptoStreamMode.Write))
                {

                    // By encrypting a chunk at
                    // a time, you can save memory
                    // and accommodate large files.
                    int count = 0;
                    int offset = 0;
                    long totalBlocks = fInfo.Length;
                    highestPercentageReached = 0;

                    // blockSizeBytes can be any arbitrary size.
                    int blockSizeBytes = rjndl.BlockSize / 8;
                    byte[] data = new byte[blockSizeBytes];
                    int bytesRead = 0;

                    using (FileStream inFs = new FileStream(inFile, FileMode.Open))
                    {                        
                        //UpdateStatus(progressBar.Maximum.ToString());
                        do
                        {
                            count = inFs.Read(data, 0, blockSizeBytes);
                            offset += count;
                            outStreamEncrypted.Write(data, 0, count);
                            bytesRead += blockSizeBytes;
                            float tmp = (float)bytesRead / ((float)totalBlocks) * 100;
                            int percentComplete = (int)tmp;
                            if (percentComplete > highestPercentageReached)
                            {
                                highestPercentageReached = percentComplete;
                                worker.ReportProgress(percentComplete);
                            }
                        }
                        while (count > 0);
                        inFs.Close();
                    }
                    outStreamEncrypted.FlushFinalBlock();
                    outStreamEncrypted.Close();
                }
                outFs.Close();
            }
            return "Completed";
        }

        private string DecryptFile(FileInfo fInfo, BackgroundWorker worker, DoWorkEventArgs e)
        {

            string inFile = fInfo.Name;
            // Create instance of Rijndael for
            // symetric decryption of the data.
            RijndaelManaged rjndl = new RijndaelManaged();
            rjndl.KeySize = 256;
            rjndl.BlockSize = 256;
            rjndl.Mode = CipherMode.CBC;

            // Create byte arrays to get the length of
            // the encrypted key and IV.
            // These values were stored as 4 bytes each
            // at the beginning of the encrypted package.
            byte[] LenK = new byte[4];
            byte[] LenIV = new byte[4];

            // Consruct the file name for the decrypted file.
            string outFile = fInfo.Directory.FullName +"\\"+ inFile.Substring(0, inFile.LastIndexOf(".")) + fInfo.Extension.Replace("_enc", "");

            // Use FileStream objects to read the encrypted
            // file (inFs) and save the decrypted file (outFs).
            using (FileStream inFs = new FileStream(fInfo.Directory.FullName +"\\"+ inFile, FileMode.Open))
            {

                inFs.Seek(0, SeekOrigin.Begin);
                inFs.Seek(0, SeekOrigin.Begin);
                inFs.Read(LenK, 0, 3);
                inFs.Seek(4, SeekOrigin.Begin);
                inFs.Read(LenIV, 0, 3);

                // Convert the lengths to integer values.
                int lenK = BitConverter.ToInt32(LenK, 0);
                int lenIV = BitConverter.ToInt32(LenIV, 0);

                // Determine the start postition of
                // the ciphter text (startC)
                // and its length(lenC).
                int startC = lenK + lenIV + 8;
                int lenC = (int)inFs.Length - startC;

                // Create the byte arrays for
                // the encrypted Rijndael key,
                // the IV, and the cipher text.
                byte[] KeyEncrypted = new byte[lenK];
                byte[] IV = new byte[lenIV];

                // Extract the key and IV
                // starting from index 8
                // after the length values.
                inFs.Seek(8, SeekOrigin.Begin);
                inFs.Read(KeyEncrypted, 0, lenK);
                inFs.Seek(8 + lenK, SeekOrigin.Begin);
                inFs.Read(IV, 0, lenIV);
                //Directory.CreateDirectory(DecrFolder);
                // Use RSACryptoServiceProvider
                // to decrypt the Rijndael key.
                byte[] KeyDecrypted = rsa.Decrypt(KeyEncrypted, false);

                // Decrypt the key.
                ICryptoTransform transform = rjndl.CreateDecryptor(KeyDecrypted, IV);

                // Decrypt the cipher text from
                // from the FileSteam of the encrypted
                // file (inFs) into the FileStream
                // for the decrypted file (outFs).
                using (FileStream outFs = new FileStream(outFile, FileMode.Create))
                {

                    int count = 0;
                    int offset = 0;
                    long totalBlocks = fInfo.Length;
                    highestPercentageReached = 0;

                    // blockSizeBytes can be any arbitrary size.
                    int blockSizeBytes = rjndl.BlockSize / 8;
                    byte[] data = new byte[blockSizeBytes];
                    int bytesRead = 0;

                    // By decrypting a chunk a time,
                    // you can save memory and
                    // accommodate large files.

                    // Start at the beginning
                    // of the cipher text.
                    inFs.Seek(startC, SeekOrigin.Begin);
                    using (CryptoStream outStreamDecrypted = new CryptoStream(outFs, transform, CryptoStreamMode.Write))
                    {
                        do
                        {
                            count = inFs.Read(data, 0, blockSizeBytes);
                            offset += count;
                            outStreamDecrypted.Write(data, 0, count);
                            bytesRead += blockSizeBytes;
                            float tmp = (float)bytesRead / ((float)totalBlocks) * 100;
                            int percentComplete = (int)tmp;
                            if (percentComplete > highestPercentageReached)
                            {
                                highestPercentageReached = percentComplete;
                                worker.ReportProgress(percentComplete);
                            }
                        }
                        while (count > 0);
                        
                        outStreamDecrypted.FlushFinalBlock();
                        outStreamDecrypted.Close();
                    }
                    outFs.Close();
                }
                inFs.Close();
            }
            return "Completed";
        }

        private void EnableButtons(Boolean status)
        {
            buttonEncryptFile.Enabled = status;
            buttonDecryptFile.Enabled = status;
        }

        private void ButtonEncryptFile_Click_1(object sender, EventArgs e)
        {
            if (rsa == null)
            {
                UpdateStatus("Key not set.");
            }
            else
            {
                try
                {
                    if (SelectedFileLocation != null)
                    {
                        FileInfo fInfo = new FileInfo(SelectedFileLocation);
                        EnableButtons(false);
                        backgroundEncrypter.RunWorkerAsync(fInfo);
                        //EncryptFile(fInfo);
                    }
                    else
                    {
                        if (openFileDialog1.ShowDialog() == DialogResult.OK)
                        {
                            string fName = openFileDialog1.FileName;
                            richTextBox.Text = "Selected File : " + fName;
                            if (fName != null)
                            {
                                FileInfo fInfo = new FileInfo(fName);
                                EnableButtons(false);
                                backgroundEncrypter.RunWorkerAsync(fInfo);
                                //EncryptFile(fInfo);
                            }
                        }
                    }
                }catch(Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error while encrypting", MessageBoxButtons.OK);
                }
            }
        }

        private void ButtonDecryptFile_Click_1(object sender, EventArgs e)
        {
            if (rsa == null)
            {
                UpdateStatus("Key not set.");
            }
            else
            {
                try
                {
                    if (SelectedFileLocation != null)
                    {
                        FileInfo fInfo = new FileInfo(SelectedFileLocation);
                        EnableButtons(false);
                        backgroundDecrypter.RunWorkerAsync(fInfo);
                        //DecryptFile(fInfo);
                    }
                    else
                    {
                        if (openFileDialog2.ShowDialog() == DialogResult.OK)
                        {
                            string fName = openFileDialog2.FileName;
                            richTextBox.Text = "Selected File : " + fName;
                            if (fName != null)
                            {
                                FileInfo fInfo = new FileInfo(fName);
                                EnableButtons(false);
                                backgroundDecrypter.RunWorkerAsync(fInfo);
                                //DecryptFile(fInfo);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error while encrypting", MessageBoxButtons.OK);
                }
            }
        }

        private void addContextEnrty()
        {
            RegistryKey _key = Registry.ClassesRoot.OpenSubKey(@"*\shell\", true);
            RegistryKey newkey = _key.CreateSubKey(rightClickText);
            RegistryKey subNewkey = newkey.CreateSubKey("command");
            subNewkey.SetValue("", Application.ExecutablePath + " \"%L\"");
            subNewkey.Close();
            newkey.Close();
            _key.Close();
        }

        private void removeContextEntry()
        {
            RegistryKey _key = Registry.ClassesRoot.OpenSubKey(@"*\shell", true);
            _key.DeleteSubKeyTree(rightClickText);
            _key.Close();
        }

        private void UpdateStatus(String text)
        {
            statusStrip1.Items[0].Text = text != null ? text : "";
        }
        private void AboutApplicationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("About. Yet to be determined");
        }

        private void ContributeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/naveenrobo/Locker");
        }

        public static bool IsAdministrator => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

        private void RightClickOptionMenuItem_Click(object sender, EventArgs e)
        {
            if (IsAdministrator)
            {
               try
                {
                    if (!rightClickOptionMenuItem.Checked)
                    {
                        this.addContextEnrty();
                        rightClickOptionMenuItem.Checked = true;
                        this.UpdateStatus("Right Click Option added");
                    }
                    else
                    {
                        this.removeContextEntry();
                        rightClickOptionMenuItem.Checked = false;
                        this.UpdateStatus("Right Click Option removed");
                    }
                }catch(Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            else
            {
                MessageBox.Show("You have to run the Locker as Administrator to enable/disable Right Click Menu Entry", "Need Admin Access", MessageBoxButtons.OK);
            }
        }

        private void GenerateNewKeysToolStripMenuItem_Click(object sender, EventArgs e)
        {
            KeysForm keysForm = new KeysForm();
            keysForm.ShowDialog();
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private string loadFile()
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.FileName = "Select a key file";
            openFileDialog1.Filter = "Text files (*.txt)|*.txt";
            openFileDialog1.Title = "Load Key file";

            String line = null;
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var filePath = openFileDialog1.FileName;
                    using (StreamReader sr = new StreamReader(openFileDialog1.FileName))
                    {
                        line = sr.ReadToEnd();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error message: {ex.Message}\n\n" +
                    $"Details:\n\n{ex.StackTrace}","Error",MessageBoxButtons.OK);
                }
            }

            return line;
        }

        private void LoadKeyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string content = this.loadFile();
            if(content != null)
            {
                try
                {
                    cspp.KeyContainerName = keyName;
                    rsa = new RSACryptoServiceProvider(cspp);
                    rsa.FromXmlString(content);
                    rsa.PersistKeyInCsp = true;
                    if (rsa.PublicOnly == true)
                        statusStrip.Text = "Key: " + cspp.KeyContainerName + " - Public Only";
                    else
                        statusStrip.Text = "Key: " + cspp.KeyContainerName + " - Full Key Pair";
                }catch(Exception ex)
                {
                    MessageBox.Show($"Error message: {ex.Message}\n\n" +
                    $"Details:\n\n{ex.StackTrace}", "Error", MessageBoxButtons.OK);
                    statusStrip.Text = "Unable to load the selected file";
                }
            }
            else
            {
                statusStrip.Text = "No content in the file";
            }

        }
        public string CurrentVersion
        {
            get
            {
                return ApplicationDeployment.IsNetworkDeployed
                       ? ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString()
                       : Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

    }



}
