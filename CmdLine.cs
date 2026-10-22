using System;
using System.Collections.Generic;

// Command line parameters processor
// Supports:
//  fixed arguments that are implicit (require no name) and required
//  -name int           optional range check, negative values don't work
//  -name string        quotes are allowed

namespace CmdLine
{
    public enum EUsage { Fixed = 0, Required, Optional };

    /// <summary>
    /// Represents an error occuring during command line parsing.
    /// </summary>
    public class CmdLineException : Exception
    {
        public CmdLineException(string parameter, string message)
            :
            base(String.Format("Syntax error of parameter {0}: {1}", parameter, message))
        {
        }
        public CmdLineException(string message)
            :
            base(message)
        {
        }
    }

    /// <summary>
    /// Represents a command line parameter. 
    /// Parameters are words in the command line beginning with a hyphen (-).
    /// The value of the parameter is the next word in
    /// </summary>
    public class CmdLineParameter
    {
        private string _name;
        private string _value = "";
        private EUsage _usage = EUsage.Optional;
        private string _helpMessage = "";
        private bool _exists = false;
        protected bool _isSwitch = false;

        /// <summary>
        /// Creates a new instance of this class.
        /// </summarèy>
        /// <param name="name">Name of parameter.</param>
        /// <param name="required">Require that the parameter is present in the command line.</param>
        /// <param name="helpMessage">The explanation of the parameter to add to the help screen.</param>
        public CmdLineParameter(string name, EUsage usage, string helpMessage)
        {
            _name = name;
            _usage = usage;
            _helpMessage = helpMessage;
        }

        /// <summary>
        /// Sets the value of the parameter.
        /// </summary>
        /// <param name="value">A string containing a integer expression.</param>
        public virtual void SetValue(string value)
        {
            _value = value;
            _exists = true;
        }
       
        /// <summary>
        /// Returns the value of the parameter.
        /// </summary>
        public string Value
        {
            get { return _value; }
        }

        /// <summary>
        /// Returns the help message associated with the parameter.
        /// </summary>
        public string Help
        {
            get { return _helpMessage; }
        }

        /// <summary>
        /// Returns true if the parameter was found in the command line.
        /// </summary>
        public bool Exists
        {
            get { return _exists; }
        }

        /// <summary>
        /// Returns if the parameter is fixed, required or optional
        /// </summary>
        public EUsage Usage
        {
            get { return _usage; }
        }

        /// <summary>
        /// Returns the name of the parameter.
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// Returns if the parameter is a switch
        /// </summary>
        public bool IsSwitch
        {
            get { return _isSwitch; }
        }

    }

    /// <summary>
    /// Represents an integer command line parameter. 
    /// </summary>
    public class CmdLineInt : CmdLineParameter
    {
        private int _min = int.MinValue;
        private int _max = int.MaxValue;
        private int _value;

        /// <summary>
        /// Creates a new instance of this class.
        /// </summary>
        /// <param name="name">Name of parameter.</param>
        /// <param name="required">Require that the parameter is present in the command line.</param>
        /// <param name="helpMessage">The explanation of the parameter to add to the help screen.</param>
        public CmdLineInt(string name, EUsage usage, string helpMessage)
            : base(name, usage, helpMessage)
        {
        }

        /// <summary>
        /// Creates a new instance of this class.
        /// </summary>
        /// <param name="name">Name of parameter.</param>
        /// <param name="required">Require that the parameter is present in the command line.</param>
        /// <param name="helpMessage">The explanation of the parameter to add to the help screen.</param>
        /// <param name="min">The minimum value of the parameter.</param>
        /// <param name="max">The maximum valie of the parameter.</param>
        public CmdLineInt(string name, EUsage usage, string helpMessage, int min, int max)
            : base(name, usage, helpMessage)
        {
            _min = min;
            _max = max;
        }

        /// <summary>
        /// Sets the value of the parameter.
        /// </summary>
        /// <param name="value">A string containing a integer expression.</param>
        public override void SetValue(string value)
        {
            base.SetValue(value);
            int i = 0;
            try
            {
                i = Convert.ToInt32(value);
            }
            catch (Exception)
            {
                throw new CmdLineException(base.Name, "Value is not an integer.");
            }
            if ((i < _min) || (i > _max))
            {
                throw new CmdLineException("-"+base.Name, String.Format("Value out of range ({0}..{1})", _min, _max));
            }

            _value = i;
        }

        /// <summary>
        /// Returns the current value of the parameter.
        /// </summary>
        new public int Value
        {
            get { return _value; }
        }

        /// <summary>
        /// A implicit converion to a int data type.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static implicit operator int(CmdLineInt s)
        {
            return s.Value;
        }
    }

    /// <summary>
    /// Represents an string command line parameter.
    /// </summary>
    public class CmdLineString : CmdLineParameter
    {
        public CmdLineString(string name, EUsage usage, string helpMessage)
            : base(name, usage, helpMessage)
        {
        }
        public static implicit operator string(CmdLineString s)
        {
            return s.Value;
        }
    }

        /// <summary>
    /// Represents an command line switch
    /// </summary>
    public class CmdLineSwitch : CmdLineParameter
    {
        public CmdLineSwitch(string name, EUsage usage, string helpMessage)
            : base(name, usage, helpMessage)
        {
            _isSwitch = true;
        }
    }

    /// <summary>
    /// Provides a simple strongly typed interface to work with command line parameters.
    /// </summary>
    public class CmdLine
    {
        // A private dictonary containing the parameters.
        private Dictionary<string, CmdLineParameter> parameters = new Dictionary<string, CmdLineParameter>();

        // Must count fixed arguments
        int FixedArgsCount;

        /// <summary>
        /// Creats a new empty command line object.
        /// </summary>
        public CmdLine()
        {
        }

        /// <summary>
        /// Returns a command line parameter by the name.
        /// </summary>
        /// <param name="name">The name of the parameter (the word after the initial hyphen (-).</param>
        /// <returns>A reference to the named comman line object.</returns>
        public CmdLineParameter this[string name]
        {
            get
            {
                if (!parameters.ContainsKey(name))
                    throw new CmdLineException(name, "Not a registered parameter."); 
                return parameters[name];
            }
        }

        /// <summary>
        /// Registers a parameter to be used and adds it to the help screen.
        /// </summary>
        /// <param name="p">The parameter to add.</param>
        public void RegisterParameter(CmdLineParameter parameter)
        {
            if (parameters.ContainsKey(parameter.Name))
                throw new CmdLineException(parameter.Name, "Parameter is already registered.");
            parameters.Add(parameter.Name, parameter);
        }

        /// <summary>
        /// Registers parameters to be used and adds hem to the help screen.
        /// </summary>
        /// <param name="p">The parameter to add.</param>
        public void RegisterParameter(CmdLineParameter[] parameters)
        {
            foreach (CmdLineParameter p in parameters)
                RegisterParameter(p);
        }


        /// <summary>
        /// Parses the command line and sets the value of each registered parmaters.
        /// </summary>
        /// <param name="args">The arguments array sent to main()</param>
        /// <returns>Error string.</returns>
        public string Parse(string[] args)
        {
            int i = 0;

            FixedArgsCount = 0;

            while (i < args.Length)
            {
                if ((args[i].Length >= 2) && (args[i][0] == '-'))
                {
                    // it's a parameter
                    // The current string is a parameter name
                    string key = args[i].Substring(1, args[i].Length - 1).ToLower();
                    string value = "";
                    i++;

                    // knwon parameter?
                    if (!parameters.ContainsKey(key))
                        throw new CmdLineException("-"+key, "Invalid parameter.");

                    // is it a switch or a parameter with value?
                    if (parameters[key].IsSwitch)
                    {
                        // it's a switch (no value)
                        // nothing to do here
                    }
                    else
                    {
                        // it's a parameter with a value
                        if ( (i < args.Length) && (args[i].Length > 0) && (args[i][0] != '-'))
                        {
                            // The next string is a value, read the value and move forward
                            value = args[i];
                            i++;
                        }
                        else
                        {
                            // Value is missing, the next string is a new parameter
                            throw new CmdLineException("-"+key, "Parameter value missing.");
                        }
                    }

                    if (parameters[key].Exists)
                        throw new CmdLineException("-"+key, "Parameter is specified more than once.");
                    
                    parameters[key].SetValue(value);
                }
                else
                {
                    // it's a fixed argument
                    // store it
                    if (StoreFixedArgument(args[i]) == false)
                    {
                        throw new CmdLineException(args[i], "Too many arguments.");
                    }
                    i++;
                }
            }

            // Check that fixed and required parameters are present in the command line. 
            foreach (string key in parameters.Keys)
            {
                if ((parameters[key].Usage == EUsage.Fixed) && !parameters[key].Exists)
                {
                    throw new CmdLineException(key, "Fixed argument is missing.");
                }

                if ((parameters[key].Usage == EUsage.Required) && !parameters[key].Exists)
                {
                    throw new CmdLineException("-" + key, "Required parameter is missing.");
                }
            }

            return "";
        }

        bool StoreFixedArgument(string value)
        {
            // find where to store it
            // fixed arguments are used without name so we must count them

            bool bStored = false;
            int index = 0;

            foreach (CmdLineParameter p in parameters.Values)
            {
                if (p.Usage == EUsage.Fixed)
                {
                    if (index == FixedArgsCount)
                    {
                        p.SetValue(value);
                        ++FixedArgsCount;
                        bStored = true;
                        break;
                    }
                    ++index;
                }
            }

            return bStored;
        }
        
        /// <summary>
        /// Generates the help screen.
        /// </summary>
        public string HelpScreen()
        {
            // get the length of the longest parameter name
            int len = 0;
            foreach (string key in parameters.Keys)
                len = Math.Max(len, key.Length);
            int tab = len + 4;

            // make a list of all parameters
            string help = "Parameters:\n";
            // List the fixed arguments first, with "<..>", not '-'
            foreach (string key in parameters.Keys)
            {
                if (parameters[key].Usage == EUsage.Fixed)
                {
                    string s = "<" + parameters[key].Name + ">";
                    while (s.Length < tab)
                        s += " ";
                    s += "Fixed,    " + parameters[key].Help + "\n";
                    help += s;
                }
            }
            // then list other arguments
            foreach (string key in parameters.Keys)
            {
                if (parameters[key].Usage != EUsage.Fixed)
                {
                    string s = "-" + parameters[key].Name;
                    while (s.Length < tab)
                        s += " ";
                    s += "Optional, "+ parameters[key].Help + "\n";
                    help += s;
                }
            }
            return help;
        }
    }

    /// <summary>
    /// Represents a CmdLine object to use with console applications.
    /// The -help parameter will be registered automatically.
    /// Any errors will be written to the console instead of generating exceptions.
    /// </summary>
    public class ConsoleCmdLine : CmdLine
    {
        public ConsoleCmdLine()
        {
            base.RegisterParameter(new CmdLineSwitch("help", EUsage.Optional, "Prints the help screen."));
        }
        public new string Parse(string[] args)
        {
            string error = "";
            try
            {
                base.Parse(args);
            }
            catch(CmdLineException ex)
            {
                error = ex.Message;
            }
            
            if(this["help"].Exists)
            {
                //foreach(string s in base.HelpScreen().Split('\n'))
                //    Console.WriteLine(s);
                Console.Write(base.HelpScreen());
                System.Environment.Exit(0);
            }

            if (error != "")
            {
                Console.WriteLine(error);
                Console.WriteLine("Use -help for more information.");
#if !UNITTEST
                System.Environment.Exit(1);
#endif
            }
            return error;
        }
    }

}