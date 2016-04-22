//  Programmer: Ludvik Jerabek
//        Date: 08\23\2010
//     Purpose: Allow INI manipulation in .NET
//  Ported to C# by Oskar Berggren, 2016-04-20

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace RDSFactor
{
    /// <summary>
    /// IniFile class used to read and write ini files by loading the file into memory
    /// </summary>
    public class IniFile
    {
        /// List of IniSection objects keeps track of all the sections in the INI file
        private readonly Dictionary<string, IniSection> m_sections;

        public IniFile()
        {
            m_sections = new Dictionary<string, IniSection>(StringComparer.InvariantCultureIgnoreCase);
        }


        /// <summary>
        /// Loads the Reads the data in the ini file into the IniFile object
        /// </summary>
        public void Load(string sFileName, bool bMerge = false)
        {
            if (!bMerge)
                RemoveAllSections();

            //  Clear the object... 
            IniSection tempsection = null;
            StreamReader oReader = new StreamReader(sFileName);
            Regex regexcomment = new Regex("^([\\s]*#.*)", (RegexOptions.Singleline | RegexOptions.IgnoreCase));
            // Broken but left for history
            //Regex regexsection = new Regex("\[[\s]*([^\[\s].*[^\s\]])[\s]*\]", (RegexOptions.Singleline Or RegexOptions.IgnoreCase))
            Regex regexsection = new Regex(@"^[\s]*\[[\s]*([^\[\s].*[^\s\]])[\s]*\][\s]*$",
                (RegexOptions.Singleline | RegexOptions.IgnoreCase));
            Regex regexkey = new Regex(@"^\s*([^=\s]*)[^=]*=(.*)", (RegexOptions.Singleline | RegexOptions.IgnoreCase));
            while (!oReader.EndOfStream)
            {
                string line = oReader.ReadLine();
                if (line != string.Empty)
                {
                    Match m;
                    if (regexcomment.Match(line).Success)
                    {
                        m = regexcomment.Match(line);
                        Trace.WriteLine(string.Format("Skipping Comment: {0}", m.Groups[0].Value));
                    }
                    else if (regexsection.Match(line).Success)
                    {
                        m = regexsection.Match(line);
                        Trace.WriteLine(string.Format("Adding section [{0}]", m.Groups[1].Value));
                        tempsection = AddSection(m.Groups[1].Value);
                    }
                    else if (regexkey.Match(line).Success && tempsection != null)
                    {
                        m = regexkey.Match(line);
                        Trace.WriteLine(string.Format("Adding Key [{0}]=[{1}]", m.Groups[1].Value, m.Groups[2].Value));
                        tempsection.AddKey(m.Groups[1].Value).Value = m.Groups[2].Value;
                    }
                    else if (tempsection != null)
                    {
                        //  Handle Key without value
                        Trace.WriteLine(string.Format("Adding Key [{0}]", line));
                        tempsection.AddKey(line);
                    }
                    else
                    {
                        //  This should not occur unless the tempsection is not created yet...
                        Trace.WriteLine(string.Format("Skipping unknown type of data: {0}", line));
                    }
                }
            }
            oReader.Close();
        }


        /// <summary>
        /// Used to save the data back to the file or your choice
        /// </summary>
        /// <param name="sFileName"></param>
        public void Save(string sFileName)
        {
            StreamWriter oWriter = new StreamWriter(sFileName, false);
            foreach (IniSection s in Sections)
            {
                Trace.WriteLine(string.Format("Writing Section: [{0}]", s.Name));
                oWriter.WriteLine("[{0}]", s.Name);
                foreach (IniSection.IniKey k in s.Keys)
                {
                    if (k.Value != string.Empty)
                    {
                        Trace.WriteLine(String.Format("Writing Key: {0}={1}", k.Name, k.Value));
                        oWriter.WriteLine("{0}={1}", k.Name, k.Value);
                    }
                    else
                    {
                        Trace.WriteLine(String.Format("Writing Key: {0}", k.Name));
                        oWriter.WriteLine("{0}", k.Name);
                    }
                }
            }
            oWriter.Close();
        }


        /// <summary>
        /// Gets all the sections
        /// </summary>
        public ICollection Sections
        {
            get { return m_sections.Values; }
        }


        /// <summary>
        /// Adds a section to the IniFile object, returns a IniSection object to the new or existing object
        /// </summary>
        public IniSection AddSection(string sSection)
        {
            sSection = sSection.Trim();

            IniSection s;
            if (!m_sections.TryGetValue(sSection, out s))
            {
                s = new IniSection(this, sSection);
                m_sections[sSection] = s;
            }

            return s;
        }


        /// <summary>
        /// Removes a section by its name sSection, returns trus on success
        /// </summary>
        public bool RemoveSection(string sSection)
        {
            sSection = sSection.Trim();
            return RemoveSection(GetSection(sSection));
        }


        /// <summary>
        /// Removes section by object, returns trus on success
        /// </summary>
        public bool RemoveSection(IniSection section)
        {
            if (section != null)
            {
                try
                {
                    m_sections.Remove(section.Name);
                    return true;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.Message);
                }
            }
            return false;
        }


        /// <summary>
        /// Removes all existing sections, returns trus on success
        /// </summary>
        /// <returns></returns>
        public bool RemoveAllSections()
        {
            m_sections.Clear();
            return m_sections.Count == 0;
        }


        /// <summary>
        /// Returns an IniSection to the section by name, NULL if it was not found
        /// </summary>
        public IniSection GetSection(string sSection)
        {
            sSection = sSection.Trim();

            if (m_sections.ContainsKey(sSection))
                return m_sections[sSection];

            return null;
        }


        /// <summary>
        /// Returns a KeyValue in a certain section
        /// </summary>
        public string GetKeyValue(string sSection, string sKey)
        {
            IniSection s = GetSection(sSection);
            if (s != null)
            {
                IniSection.IniKey k = s.GetKey(sKey);
                if (k != null)
                    return k.Value;
            }
            return string.Empty;
        }


        /// <summary>
        /// Sets a KeyValuePair in a certain section
        /// </summary>
        public bool SetKeyValue(string sSection, string sKey, string sValue)
        {
            IniSection s = AddSection(sSection);
            if (s != null)
            {
                IniSection.IniKey k = s.AddKey(sKey);
                if (k != null)
                {
                    k.Value = sValue;
                    return true;
                }
            }
            return false;
        }


        /// <summary>
        /// Renames an existing section returns true on success, false if the section didn't exist or there was another section with the same sNewSection
        /// </summary>
        public bool RenameSection(string sSection, string sNewSection)
        {
            // Note string trims are done in lower calls. 
            bool bRval = false;
            IniSection s = GetSection(sSection);
            if (s != null)
                bRval = s.SetName(sNewSection);
            return bRval;
        }


        /// <summary>
        /// Renames an existing key returns true on success, false if the key didn't exist or there was another section with the same sNewKey
        /// </summary>
        public bool RenameKey(string sSection, string sKey, string sNewKey)
        {
            // Note string trims are done in lower calls.    
            IniSection s = GetSection(sSection);
            if (s != null)
            {
                IniSection.IniKey k = s.GetKey(sKey);
                if (k != null)
                    return k.SetName(sNewKey);
            }
            return false;
        }


        /// <summary>
        /// Remove a key by section name and key name
        /// </summary>
        public bool RemoveKey(string sSection, string sKey)
        {
            IniSection s = GetSection(sSection);
            if (s != null)
                return s.RemoveKey(sKey);
            return false;
        }


        public class IniSection
        {
            //  IniFile IniFile object instance
            private readonly IniFile m_pIniFile;
            //  Name of the section
            private string m_sSection;
            //  List of IniKeys in the section
            private readonly Dictionary<string, IniKey> m_keys;


            public IniSection(IniFile parent, string sSection)
            {
                m_pIniFile = parent;
                m_sSection = sSection;
                m_keys = new Dictionary<string, IniKey>(StringComparer.InvariantCultureIgnoreCase);
            }


            public ICollection<IniKey> Keys
            {
                get { return m_keys.Values; }
            }


            public string Name
            {
                get { return m_sSection; }
            }


            /// <summary>
            /// Adds a key to the IniSection object, returns a IniKey object to the new or existing object
            /// </summary>
            public IniKey AddKey(string sKey)
            {
                sKey = sKey.Trim();
                IniKey k = null;
                if (sKey.Length != 0)
                {
                    if (m_keys.ContainsKey(sKey))
                    {
                        k = m_keys[sKey];
                    }
                    else
                    {
                        k = new IniKey(this, sKey);
                        m_keys[sKey] = k;
                    }
                }
                return k;
            }


            /// <summary>
            /// Removes a single key by string
            /// </summary>
            public bool RemoveKey(string sKey)
            {
                return RemoveKey(GetKey(sKey));
            }


            /// <summary>
            /// Removes a single key by IniKey object
            /// </summary>
            public bool RemoveKey(IniKey key)
            {
                if (key != null)
                {
                    try
                    {
                        m_keys.Remove(key.Name);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine(ex.Message);
                    }
                }
                return false;
            }


            /// <summary>
            /// Removes all the keys in the section
            /// </summary>
            /// <returns></returns>
            public bool RemoveAllKeys()
            {
                m_keys.Clear();
                return m_keys.Count == 0;
            }


            /// <summary>
            /// Returns a IniKey object to the key by name, NULL if it was not found
            /// </summary>
            public IniKey GetKey(string sKey)
            {
                sKey = sKey.Trim();
                if (m_keys.ContainsKey(sKey))
                    return m_keys[sKey];
                return null;
            }


            /// <summary>
            /// Sets the section name, returns true on success, fails if the section
            /// name sSection already exists.
            /// </summary>
            public bool SetName(string sSection)
            {
                sSection = sSection.Trim();
                if (sSection.Length != 0)
                {
                    // Get existing section if it even exists...
                    IniSection s = m_pIniFile.GetSection(sSection);
                    if (s != this && s != null)
                        return false;

                    try
                    {
                        // Remove the current section
                        m_pIniFile.m_sections.Remove(m_sSection);
                        // Set the new section name to this object
                        m_pIniFile.m_sections[sSection] = this;
                        // Set the new section name
                        m_sSection = sSection;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine(ex.Message);
                    }
                }

                return false;
            }


            /// <summary>
            /// Returns the section name
            /// </summary>
            /// <returns></returns>
            public string GetName()
            {
                return m_sSection;
            }


            public class IniKey
            {
                //  Name of the Key
                private string m_sKey;
                //  Value associated
                private string m_sValue;
                //  Pointer to the parent CIniSection
                private readonly IniSection m_section;


                internal IniKey(IniSection parent, string sKey)
                {
                    m_section = parent;
                    m_sKey = sKey;
                }


                public string Name
                {
                    get { return m_sKey; }
                }


                /// <summary>
                /// Sets or Gets the value of the key
                /// </summary>
                public string Value
                {
                    get { return m_sValue; }
                    set { m_sValue = value; }
                }


                /// <summary>
                /// Sets the value of the key
                /// </summary>
                public void SetValue(string sValue)
                {
                    m_sValue = sValue;
                }


                /// <summary>
                /// Returns the value of the Key
                /// </summary>
                public string GetValue()
                {
                    return m_sValue;
                }


                /// <summary>
                /// Sets the key name
                /// </summary>
                /// <returns>Returns true on success, fails if the section name sKey already exists</returns>
                public bool SetName(string sKey)
                {
                    sKey = sKey.Trim();
                    if (sKey.Length != 0)
                    {
                        IniKey k = m_section.GetKey(sKey);
                        if (k != this && k != null)
                            return false;
                        try
                        {
                            // Remove the current key
                            m_section.m_keys.Remove(m_sKey);
                            // Set the new key name to this object
                            m_section.m_keys[sKey] = this;
                            // Set the new key name
                            m_sKey = sKey;
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine(ex.Message);
                        }
                    }
                    return false;
                }


                /// <summary>
                /// Returns the name of the Key
                /// </summary>
                public string GetName()
                {
                    return m_sKey;
                }
            } // End of IniKey class
        } // End of IniSection class
    }
} //  End of IniFile class

