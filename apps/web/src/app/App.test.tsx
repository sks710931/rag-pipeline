import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';

describe('App Shell', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(globalThis, 'fetch').mockImplementation(
      () => new Promise(() => {})
    );
  });

  it('renders skip link targeting main-content', () => {
    render(<App />);

    const skipLink = screen.getByRole('link', {
      name: /skip to main content/i,
    });
    expect(skipLink).toBeInTheDocument();
    expect(skipLink).toHaveAttribute('href', '#main-content');
  });

  it('renders header landmark with application title', () => {
    render(<App />);

    const banner = screen.getByRole('banner');
    expect(banner).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { level: 1, name: /pixel rag/i })
    ).toBeInTheDocument();
  });

  it('renders main landmark with matching id and accessible tabIndex', () => {
    render(<App />);

    const main = screen.getByRole('main');
    expect(main).toBeInTheDocument();
    expect(main).toHaveAttribute('id', 'main-content');
    expect(main).toHaveAttribute('tabIndex', '-1');
  });
});
