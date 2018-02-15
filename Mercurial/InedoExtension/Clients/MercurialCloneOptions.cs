using System;

namespace Inedo.Extensions.Shared.Mercurial.Clients
{
    [Serializable]
    public sealed class MercurialCloneOptions
    {
        public MercurialCloneOptions()
        {
        }

        public string Branch { get; set; }

        public override string ToString()
        {
            return $"Branch={this.Branch ?? "(default)"}";
        }
    }
}
