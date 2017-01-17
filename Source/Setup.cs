using System;
using System.Windows.Forms;
using centrafuse.Plugins;

namespace TimeZone
{
    public class Setup : CFSetup
    {

#region Construction

        // The setup constructor will be called each time this plugin's setup is opened from the CF Setting Page
        // This setup is opened as a dialog from the CF_pluginShowSetup() call into the main plugin application form.
        public Setup(ICFMain mForm, ConfigReader config, LanguageReader lang)
        {
            // MainForm must be set before calling any Centrafuse API functions
            this.MainForm = mForm;

            // pluginConfig and pluginLang should be set before calling CF_initSetup() so this CFSetup instance 
            // will internally save any changed settings.
            this.pluginConfig = config;
            this.pluginLang = lang;

            // When CF_initSetup() is called, the CFPlugin layer will call back into CF_setupReadSettings() to read the page         
            CF_initSetup(1, 1);

            // Update the Settings page title
            this.CF_updateText("TITLE", this.pluginLang.ReadField("/APPLANG/SETUP/TITLE"));
        }

#endregion

#region CFSetup

        public override void CF_setupReadSettings(int currentpage, bool advanced)
        {
            try
            {
                int i = CFSetupButton.One;

                if (currentpage == 1)
                {
                    // TEXT BUTTONS (1-4)
                    ButtonHandler[i] = new CFSetupHandler(SetDisplayName);
                    ButtonText[i] = this.pluginLang.ReadField("APPLANG/SETUP/DISPLAYNAME");
                    ButtonValue[i++] = this.pluginLang.ReadField("APPLANG/TIMEZONE/DISPLAYNAME");

                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";

                    // BOOL BUTTONS (5-8)
                    ButtonHandler[i] = new CFSetupHandler(SetLogEvents);
                    ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/LOGEVENTS");
                    ButtonValue[i++] = this.pluginConfig.ReadField("/APPCONFIG/LOGEVENTS");

                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

#endregion

#region User Input Events

        private void SetDisplayName(ref object value)
        {
            try
            {
                object tempobject;
                string resultvalue, resulttext;

                // Display OSK for user to type display name
                if (this.CF_systemDisplayDialog(CF_Dialogs.OSK, this.pluginLang.ReadField("/APPLANG/SETUP/DISPLAYNAME"), ButtonValue[(int)value], null, out resultvalue, out resulttext, out tempobject, null, true, true, true, true, false, false, 1) == DialogResult.OK)
                {
                    // save user value, note this does not save to file yet, as this should only be done when user confirms settings
                    // being overwritten when they click the "Save" button.  Saving is done internally by the CFSetup instance if
                    // pluginConfig and pluginLang were properly set before callin CF_initSetup().
                    this.pluginLang.WriteField("/APPLANG/TIMEZONE/DISPLAYNAME", resultvalue);

                    // Display new value on Settings Screen button
                    ButtonValue[(int)value] = resultvalue;
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

        private void SetLogEvents(ref object value)
        {
            this.pluginConfig.WriteField("/APPCONFIG/LOGEVENTS", value.ToString());
        }

#endregion

    }
}
