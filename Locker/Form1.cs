using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.IO;
using Microsoft.Win32;
using System.Security.Principal;

namespace Locker
{
    public partial class LockerForm : Form
    {
        // Declare CspParmeters and RsaCryptoServiceProvider
        // objects with global scope of your Form class.
        CspParameters cspp = new CspParameters();
        RSACryptoServiceProvider rsa;

        // Path variables for source, encryption, and
        // decryption folders. Must end with a backslash.
        const string EncrFolder = @"c:\Encrypt\";
        const string DecrFolder = @"c:\Decrypt\";
        const string SrcFolder = @"c:\docs\";

        // Public key file
        const string PubKeyFile = @"c:\encrypt\rsaPublicKey.txt";
        const string FullKeyFile = @"c:\encrypt\rsaFullKey.txt";

        // Key container name for
        // private/public key value pair.
        const string keyName = "LockerKey";

        private Boolean rightClickState = false;
        private string rightClickText = "Encrypt/Decrypt Folder";

        public LockerForm()
        {
            InitializeComponent();
           
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            RegistryKey _key = Registry.ClassesRoot.OpenSubKey($"Folder\\Shell\\{rightClickText}", false);
            rightClickState = _key == null ? false : true;
            rightClickOptionMenuItem.Checked = rightClickState;
        }

        private void EncryptFile(FileInfo fInfo)
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
            // Change the file's extension to ".enc"
            string outFile = EncrFolder + inFile.Substring(startFileName, inFile.LastIndexOf(".") - startFileName) +fInfo.Extension;

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

                    // blockSizeBytes can be any arbitrary size.
                    int blockSizeBytes = rjndl.BlockSize / 8;
                    byte[] data = new byte[blockSizeBytes];
                    int bytesRead = 0;

                    using (FileStream inFs = new FileStream(inFile, FileMode.Open))
                    {
                        do
                        {
                            count = inFs.Read(data, 0, blockSizeBytes);
                            offset += count;
                            outStreamEncrypted.Write(data, 0, count);
                            bytesRead += blockSizeBytes;
                        }
                        while (count > 0);
                        inFs.Close();
                    }
                    outStreamEncrypted.FlushFinalBlock();
                    outStreamEncrypted.Close();
                }
                outFs.Close();
            }

        }

        private void DecryptFile(FileInfo fInfo)
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
            string outFile = DecrFolder + inFile.Substring(0, inFile.LastIndexOf(".")) + fInfo.Extension;

            // Use FileStream objects to read the encrypted
            // file (inFs) and save the decrypted file (outFs).
            using (FileStream inFs = new FileStream(EncrFolder + inFile, FileMode.Open))
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
                Directory.CreateDirectory(DecrFolder);
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

                    // blockSizeBytes can be any arbitrary size.
                    int blockSizeBytes = rjndl.BlockSize / 8;
                    byte[] data = new byte[blockSizeBytes];


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

                        }
                        while (count > 0);

                        outStreamDecrypted.FlushFinalBlock();
                        outStreamDecrypted.Close();
                    }
                    outFs.Close();
                }
                inFs.Close();
            }

        }

        private void ButtonEncryptFile_Click_1(object sender, EventArgs e)
        {
            if (rsa == null)
                MessageBox.Show("Key not set.");
            else
            {

                // Display a dialog box to select a file to encrypt.
                openFileDialog1.InitialDirectory = SrcFolder;
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    string fName = openFileDialog1.FileName;
                    if (fName != null)
                    {
                        FileInfo fInfo = new FileInfo(fName);
                        // Pass the file name without the path.
                       
                        EncryptFile(fInfo);
                    }
                }
            }
        }

        private void ButtonDecryptFile_Click_1(object sender, EventArgs e)
        {
            if (rsa == null)
                MessageBox.Show("Key not set.");
            else
            {
                // Display a dialog box to select the encrypted file.
                openFileDialog2.InitialDirectory = EncrFolder;
                if (openFileDialog2.ShowDialog() == DialogResult.OK)
                {
                    string fName = openFileDialog2.FileName;
                    if (fName != null)
                    {
                        FileInfo fi = new FileInfo(fName);
                        
                        DecryptFile(fi);
                    }
                }
            }

        }

        private void addContextEnrty()
        {
            RegistryKey _key = Registry.ClassesRoot.OpenSubKey("Folder\\Shell", true);
            RegistryKey newkey = _key.CreateSubKey(rightClickText);
            RegistryKey subNewkey = newkey.CreateSubKey("Command");
            subNewkey.SetValue("", Application.ExecutablePath);
            subNewkey.Close();
            newkey.Close(); 
            _key.Close();
        }

        private void removeContextEntry()
        {
            RegistryKey _key = Registry.ClassesRoot.OpenSubKey("Folder\\Shell", true);
            _key.DeleteSubKey($"{rightClickText}\\Command");
            _key.DeleteSubKey(rightClickText);
            _key.Close();
        }

        private void UpdateStatus(String text)
        {
            if (text != null)
            {
                this.statusStrip1.Items[0].Text = text;
            }
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
                MessageBox.Show("You have to run the Locker as Administrator to enable this functionality", "Need Admin Access", MessageBoxButtons.OK);
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
                }
            }
            else
            {
                statusStrip.Text = "Unable to load the selected file";
            }

        }

    }
}
