using GenericMCPServerBackend.Core.Misc;
using GRYLibrary.Core.Logging.GRYLogger;
using GRYLibrary.Core.Logging.GRYLogger.ConcreteLogTargets;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Console = GRYLibrary.Core.Logging.GRYLogger.ConcreteLogTargets.Console;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// Keeps the token of the administrator of this server, in the same way a client keeps the token with which it
    /// administrates itself: the token exists in a readable form exactly once, and only its hash is stored.
    ///
    /// That once is the moment it is created, and it is written to the log while the file-log is switched off, so it
    /// reaches the person who starts the server and nothing else. Whoever missed it deletes the file and starts the
    /// server again; the token itself can not be looked up anywhere.
    /// </summary>
    public sealed class AdminTokenService : IAdminTokenService
    {
        /// <summary>The name of the file in the configuration-folder.</summary>
        public const string FileName = "AdminToken.csv";

        internal const string Header = "admintokenhash";

        private readonly CsvFile _File;

        /// <param name="file">The file which holds the hash of the token.</param>
        /// <param name="logger">The log the token is written to when it is created.</param>
        public AdminTokenService(string file, IGRYLog logger)
        {
            this._File = new CsvFile(file, Header);
            if (this._File.ReadRows().Count == 0)
            {
                this.CreateToken(logger);
            }
        }

        /// <summary>The default location of the file: it belongs to the configuration of the server.</summary>
        public static string GetDefaultFile(string configurationFolder)
        {
            return Path.Combine(configurationFolder, FileName);
        }

        public string GetFile()
        {
            return this._File.GetFile();
        }

        public bool Matches(string token)
        {
            IReadOnlyList<string[]> rows = this._File.ReadRows();
            if (rows.Count == 0)
            {
                // A file without a token is no access: without this, an empty file would let every request pass.
                return false;
            }
            return Token.Matches(token, rows[0][0]);
        }

        /// <summary>
        /// Creates the token, stores its hash and writes the token itself to the log.
        /// </summary>
        private void CreateToken(IGRYLog logger)
        {
            string token = Token.Create(Token.PrefixOfAnAdminToken);
            this._File.WriteRows(new List<string[]>() { new string[] { Token.GetHash(token) } });
            LogTheTokenWithoutWritingItIntoAFile(logger, $"The token of the administrator of this server was created: {token}{Environment.NewLine}It is shown exactly once, here. Only its hash is stored (see \"{this.GetFile()}\"), so it can not be looked up later; whoever needs a new one deletes that file and starts the server again.");
        }

        /// <summary>
        /// Writes the given message to the log without letting it reach a file.
        /// </summary>
        /// <remarks>
        /// A token in a log-file is a token which stays readable for as long as that file exists, and log-files are
        /// copied, shipped and archived - so the file-targets are switched off while the token is written and switched
        /// back on afterwards, whatever happens in between.
        ///
        /// That only makes sense when somebody actually sees the message, so it is checked that a target which writes
        /// to the console is enabled. If there is none, the token would be created and immediately lost, and the server
        /// would run with an access nobody has: that is stated as an error instead of happening silently.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when no enabled console-target exists.</exception>
        private static void LogTheTokenWithoutWritingItIntoAFile(IGRYLog logger, string message)
        {
            List<GRYLogTarget> logTargets = logger.Configuration.LogTargets;
            if (!logTargets.OfType<Console>().Any(target => target.Enabled))
            {
                throw new InvalidOperationException("The token of the administrator can not be shown, because no enabled log-target writes to the console. Enable one and start the server again; a token which nobody reads is of no use.");
            }
            List<LogFile> enabledFileTargets = logTargets.OfType<LogFile>().Where(target => target.Enabled).ToList();
            foreach (LogFile fileTarget in enabledFileTargets)
            {
                fileTarget.Enabled = false;
            }
            try
            {
                logger.Log(message, LogLevel.Information);
            }
            finally
            {
                foreach (LogFile fileTarget in enabledFileTargets)
                {
                    fileTarget.Enabled = true;
                }
            }
        }
    }
}
