using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenTelemetryProductSvc.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenTelemetryProductSvc.Repositories
{
    public interface IProductRepository
    {
        Task AddAsync(Product product);
        Task DeleteAsync(Guid id);
        Task<IEnumerable<Product>> GetAllAsync();
        Task<Product> GetByIdAsync(Guid id);
        Task UpdateAsync(Product product);
    }
}