using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GenericMCPServerBackend.Core.Misc
{
    /// <summary>
    /// A csv-file of the configuration of this server.
    ///
    /// The clients, their roles and their tokens are kept in such files: their amount is small, an administrator can
    /// read them without a tool, and a backup of the configuration-folder contains them. A database would only add a
    /// component which has to run, be reachable and be migrated.
    ///
    /// Every file is read whenever it is needed and not held in memory, so a change works immediately and without a
    /// restart of the server, and the file stays the only place which decides.
    /// </summary>
    internal sealed class CsvFile
    {
        private const char Separator = ';';

        private readonly string _File;

        private readonly string _Header;

        private readonly int _AmountOfColumns;

        /// <param name="file">The file. It is created with its header when it does not exist yet, so that an administrator finds a file which shows what belongs into it instead of having to know its format.</param>
        /// <param name="header">The header-line, which also states how many columns a row has.</param>
        internal CsvFile(string file, string header)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                throw new ArgumentException("The file must be set.", nameof(file));
            }
            this._File = Path.GetFullPath(file);
            this._Header = header;
            this._AmountOfColumns = header.Split(Separator).Length;
            this.EnsureFileExists();
        }

        internal string GetFile()
        {
            return this._File;
        }

        private void EnsureFileExists()
        {
            if (File.Exists(this._File))
            {
                return;
            }
            string folder = Path.GetDirectoryName(this._File);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }
            File.WriteAllLines(this._File, new string[] { this._Header }, Encoding.UTF8);
        }

        /// <summary>
        /// Returns the rows of the file.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a line does not consist of the columns of the header. A line which can not be read is not skipped, because skipping it would silently take an access away or leave one in place, and nobody would see which of the two happened.</exception>
        internal IReadOnlyList<string[]> ReadRows()
        {
            List<string[]> result = new List<string[]>();
            if (!File.Exists(this._File))
            {
                return result;
            }
            string[] lines = File.ReadAllLines(this._File);
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                if (string.IsNullOrWhiteSpace(line) || string.Equals(line.Trim(), this._Header, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string[] columns = line.Split(Separator);
                if (columns.Length != this._AmountOfColumns)
                {
                    throw new InvalidOperationException($"Line {index + 1} of \"{this._File}\" does not consist of the {this._AmountOfColumns} values \"{this._Header}\".");
                }
                result.Add(columns.Select(column => column.Trim()).ToArray());
            }
            return result;
        }

        internal void AppendRow(params string[] columns)
        {
            this.EnsureFileExists();
            File.AppendAllLines(this._File, new string[] { string.Join(Separator, columns) }, Encoding.UTF8);
        }

        /// <summary>
        /// Writes the given rows and removes everything which was in the file before.
        /// </summary>
        internal void WriteRows(IEnumerable<string[]> rows)
        {
            List<string> lines = new List<string>() { this._Header };
            lines.AddRange(rows.Select(row => string.Join(Separator, row)));
            File.WriteAllLines(this._File, lines, Encoding.UTF8);
        }

        /// <summary>
        /// Removes every row which the given decision matches and states whether something was removed.
        /// </summary>
        internal bool RemoveRows(Func<string[], bool> rowIsRemoved)
        {
            IReadOnlyList<string[]> rows = this.ReadRows();
            List<string[]> remaining = rows.Where(row => !rowIsRemoved(row)).ToList();
            if (remaining.Count == rows.Count)
            {
                return false;
            }
            this.WriteRows(remaining);
            return true;
        }

        /// <summary>
        /// The value which may not contain the separator of a csv-file, so that one value can not become two.
        /// </summary>
        internal static bool ContainsSeparator(string value)
        {
            return value != null && value.Contains(Separator);
        }
    }
}
