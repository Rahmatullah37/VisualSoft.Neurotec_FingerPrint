using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VisualSoft.Biomatric.Identification.Domain.Models;

namespace VisualSoft.Biomatric.Identification.Services
{
    public interface IBiometricService : IDisposable
    {
        Task<IdentificationResult> IdentifyAsync(string wsqFilePath);
    }
}
