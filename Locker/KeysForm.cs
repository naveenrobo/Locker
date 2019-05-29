using System;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.IO;

namespace Locker
{
    public partial class KeysForm : Form
    {
        // Declare CspParmeters and RsaCryptoServiceProvider
        // objects with global scope of your Form class.
        CspParameters cspp = new CspParameters();
        RSACryptoServiceProvider rsa;

        const string keyName = "LockerKey";

        public KeysForm()
        {
            InitializeComponent();
        }

        private void ButtonCreateAsmKeys_Click(object sender, EventArgs e)
        {
            // Stores a key pair in the key container.
            cspp.KeyContainerName = keyName;
            rsa = new RSACryptoServiceProvider(cspp);
            rsa.PersistKeyInCsp = true;
            if (rsa.PublicOnly == true)
                this.statusText.Text = "Key: " + cspp.KeyContainerName + " - Public Only";
            else
                this.statusText.Text = "Key: " + cspp.KeyContainerName + " - Full Key Pair";

            //for emergency purpose....should be removed
            saveKeys();
            MessageBox.Show("Keys generated. Export both of them manually in a secured location","Export Keys",MessageBoxButtons.OK);
        }

        private void ButtonExportPublicKey_Click(object sender, EventArgs e)
        {

            if(rsa == null)
            {
                this.statusText.Text = "Generate the Keys first";
                return;
            }
            // Save the public key created by the RSA
            // to a file. Caution, persisting the
            // key to a file is a security risk.
           try
            {
                SaveFileDialog saveFileDialog1 = new SaveFileDialog();
                saveFileDialog1.Filter = "Text Files | *.txt";
                saveFileDialog1.Title = "Save public key";
                saveFileDialog1.ShowDialog();

                if (saveFileDialog1.FileName != "")
                {
                    StreamWriter sw = new StreamWriter(saveFileDialog1.OpenFile());
                    sw.Write(rsa.ToXmlString(false));
                    sw.Close();
                    this.statusText.Text = "Public key saved successfully";
                }
            }catch(Exception ex)
            {
                MessageBox.Show(ex.Message,"Exception",MessageBoxButtons.OK);
            }
           
        }

        private void ButtonExportPrivateKey_Click(object sender, EventArgs e)
        {
            if (rsa == null)
            {
                this.statusText.Text = "Generate the Keys first";
                return;
            }
            // Save the full key created by the RSA
            // to a file. Caution, persisting the
            // key to a file is a security risk.
            try
            {
                SaveFileDialog saveFileDialog1 = new SaveFileDialog();
                saveFileDialog1.Filter = "Text Files | *.txt";
                saveFileDialog1.Title = "Save private key";
                saveFileDialog1.ShowDialog();

                if (saveFileDialog1.FileName != "")
                {
                    StreamWriter sw = new StreamWriter(saveFileDialog1.OpenFile());
                    sw.Write(rsa.ToXmlString(true));
                    sw.Close();
                    this.statusText.Text = "Private key saved successfully";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Exception", MessageBoxButtons.OK);
            }
        }

        private void saveKeys()
        {
            // The folder for the roaming current user 
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // Combine the base folder with your specific folder....
            string lockerFolder = Path.Combine(folder, "Locker");

            // CreateDirectory will check if folder exists and, if not, create it.
            // If folder exists then CreateDirectory will do nothing.
            Directory.CreateDirectory(lockerFolder);

            WriteToFile(lockerFolder + $@"\Locker-{DateTime.Now.Ticks}-{Guid.NewGuid()}.txt", rsa.ToXmlString(false));
            WriteToFile(lockerFolder + $@"\Locker-{DateTime.Now.Ticks}-{Guid.NewGuid()}.txt", rsa.ToXmlString(true));
        }

        private void WriteToFile(String file,String content)
        {
            if (!File.Exists(file))
            {
                // Create a file to write to.
                using (StreamWriter sw = File.CreateText(file))
                {
                    sw.WriteLine(content);
                }
            }
        }
    }
}
