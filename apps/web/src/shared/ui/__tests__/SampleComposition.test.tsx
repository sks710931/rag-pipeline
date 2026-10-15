import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { axe } from 'vitest-axe';
import { SampleComposition } from '../SampleComposition';

describe('SampleComposition', () => {
  it('renders all sections and primitives without accessibility violations', async () => {
    const { container } = render(<SampleComposition />);

    expect(
      screen.getByRole('heading', {
        level: 1,
        name: /design system primitives & tokens sample composition/i,
      })
    ).toBeInTheDocument();

    // Verify presence of core primitive sections
    expect(
      screen.getByRole('heading', { name: /form inputs & validation/i })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: /button variants & states/i })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: /status indicators/i })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: /setup stepper/i })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: /accessible dialogs & overlays/i })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: /loading skeletons/i })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: /empty & error states/i })
    ).toBeInTheDocument();

    // Run axe-core smoke check across the whole rendered composition
    expect(await axe(container)).toHaveNoViolations();
  });

  it('allows opening modal and drawer overlays from sample composition', async () => {
    const user = userEvent.setup();
    render(<SampleComposition />);

    const openModalBtn = screen.getByRole('button', {
      name: /open configuration modal/i,
    });
    await user.click(openModalBtn);

    const modalDialog = await screen.findByRole('dialog');
    expect(modalDialog).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: /security preferences/i })
    ).toBeInTheDocument();

    const closeModalBtn = screen.getByRole('button', { name: 'Close modal' });
    await user.click(closeModalBtn);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    const openDrawerBtn = screen.getByRole('button', {
      name: /open audit drawer/i,
    });
    await user.click(openDrawerBtn);

    const drawerDialog = await screen.findByRole('dialog');
    expect(drawerDialog).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: /system audit log/i })
    ).toBeInTheDocument();

    const closeDrawerBtn = screen.getByRole('button', { name: 'Close drawer' });
    await user.click(closeDrawerBtn);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('documents that 320-375px responsive layout visual check is manual and not done in JSDOM', () => {
    /*
     * Architectural / Testing constraint:
     * JSDOM does not perform layout calculation, text metric layout, or bounding-box
     * geometry. Therefore, automated assertions on visual clipping, line breaks, or
     * horizontal scrolling overflow cannot be reliably performed in JSDOM.
     * Responsive 320px–375px visual inspection is noted as manual and not done in JSDOM tests.
     */
    const jsdomHasLayoutEngine =
      typeof window !== 'undefined' &&
      typeof document.body.getBoundingClientRect === 'function' &&
      document.body.getBoundingClientRect().width > 0;

    // Verify that JSDOM returns 0 for layout dimensions, confirming layout cannot be tested in JSDOM
    expect(jsdomHasLayoutEngine).toBe(false);
  });
});
