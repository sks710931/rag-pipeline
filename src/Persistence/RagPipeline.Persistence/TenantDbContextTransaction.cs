namespace RagPipeline.Persistence;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;

public sealed class TenantDbContextTransaction : IDbContextTransaction
{
    private readonly IDbContextTransaction _inner;
    private readonly RagPipelineDbContext _context;
    private bool _isDisposed;

    public TenantDbContextTransaction(IDbContextTransaction inner, RagPipelineDbContext context)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Guid TransactionId => _inner.TransactionId;

    public void Commit()
    {
        try
        {
            _inner.Commit();
        }
        finally
        {
            _context.ActiveTenantTransactionId = null;
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _inner.CommitAsync(cancellationToken);
        }
        finally
        {
            _context.ActiveTenantTransactionId = null;
        }
    }

    public void Rollback()
    {
        try
        {
            _inner.Rollback();
        }
        finally
        {
            _context.ActiveTenantTransactionId = null;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _inner.RollbackAsync(cancellationToken);
        }
        finally
        {
            _context.ActiveTenantTransactionId = null;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        try
        {
            _inner.Dispose();
        }
        finally
        {
            _context.ActiveTenantTransactionId = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        try
        {
            await _inner.DisposeAsync();
        }
        finally
        {
            _context.ActiveTenantTransactionId = null;
        }
    }
}
