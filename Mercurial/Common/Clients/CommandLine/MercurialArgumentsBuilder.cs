using System.Collections.Generic;
using System.Linq;

namespace Inedo.Extensions.Shared.Mercurial.Clients.CommandLine
{
    internal sealed class MercurialArgumentsBuilder
    {
        private List<MercurialArg> arguments = new List<MercurialArg>(16);

        public MercurialArgumentsBuilder()
        {
        }

        public MercurialArgumentsBuilder(string initialArguments)
        {
            this.Append(initialArguments);
        }

        public void Append(string arg) => this.arguments.Add(new MercurialArg(arg, false, false));
        public void AppendQuoted(string arg) => this.arguments.Add(new MercurialArg(arg, true, false));
        public void AppendSensitive(string arg) => this.arguments.Add(new MercurialArg(arg, true, true));

        public override string ToString() => string.Join(" ", this.arguments);
        public string ToSensitiveString() => string.Join(" ", this.arguments.Select(a => a.ToSensitiveString()));

        private sealed class MercurialArg
        {
            private bool quoted;
            private bool sensitive;
            private string arg;

            public MercurialArg(string arg, bool quoted, bool sensitive)
            {
                this.arg = arg ?? "";
                this.quoted = quoted;
                this.sensitive = sensitive;
            }

            public override string ToString()
            {
                if (this.quoted)
                    return '"' + this.arg.Replace("\"", @"\""") + '"';
                else
                    return this.arg;
            }

            public string ToSensitiveString()
            {
                if (this.sensitive)
                    return "(hidden)";
                else if (this.quoted)
                    return '"' + this.arg.Replace("\"", @"\""") + '"';
                else
                    return this.arg;
            }
        }
    }
}
