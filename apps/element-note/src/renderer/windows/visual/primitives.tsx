/* eslint-disable react-refresh/only-export-components */
import React, { useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import '@styles/viewer-compat-v1.css';
import '@styles/tokens.css';
import '@styles/chrome-global.css';
import '@styles/chrome-main.css';
import './primitives.css';
import Button from '@components/main/common/Button';
import Dropdown from '@components/main/common/Dropdown';
import Toggle from '@components/main/common/Toggle';
import TabSwitch from '@components/main/common/TabSwitch';
import {
  NumberInput,
  OptionalNumberInput,
} from '@components/main/common/NumberInput';
import { TextInput } from '@components/main/common/TextInput';
import Modal from '@components/main/Modal/Modal';
import FloatingPopup from '@components/main/Modal/FloatingPopup';
import FloatingTooltip from '@components/main/Modal/FloatingTooltip';

type FixtureState = 'controls' | 'dropdown' | 'modal';

const state = (new URLSearchParams(window.location.search).get('state') ??
  'controls') as FixtureState;

function ControlsFixture() {
  const [enabled, setEnabled] = useState(true);
  const [amount, setAmount] = useState(24);
  const [optional, setOptional] = useState<number | undefined>();
  const [name, setName] = useState('Key label');
  const [activePanel, setActivePanel] = useState('appearance');

  return (
    <main data-dmn-app-chrome className="primitive-stage" data-state="controls">
      <section className="primitive-card">
        <h1 className="primitive-heading">UI primitives</h1>
        <div className="primitive-row">
          <span className="primitive-label">Actions</span>
          <Button variant="primary">Apply</Button>
          <Button>Duplicate</Button>
          <Button variant="danger">Remove</Button>
          <Button loading>Saving</Button>
        </div>
        <div className="primitive-row">
          <span className="primitive-label">Number</span>
          <NumberInput
            value={amount}
            onChange={setAmount}
            min={0}
            max={100}
            suffix="px"
            ariaLabel="Amount"
          />
          <output data-testid="amount-value">{amount}</output>
          <OptionalNumberInput
            value={optional}
            onChange={setOptional}
            placeholder="Auto"
            ariaLabel="Optional amount"
          />
        </div>
        <div className="primitive-row">
          <span className="primitive-label">Text</span>
          <TextInput
            value={name}
            onChange={setName}
            ariaLabel="Key label"
            width="160px"
          />
        </div>
        <div className="primitive-row">
          <span className="primitive-label">Toggle</span>
          <Toggle
            checked={enabled}
            onChange={setEnabled}
            ariaLabel="Enable effect"
          />
          <span>{enabled ? 'Enabled' : 'Disabled'}</span>
          <Toggle
            checked={false}
            onChange={() => undefined}
            ariaLabel="Unavailable effect"
            disabled
          />
        </div>
        <div className="primitive-row">
          <span className="primitive-label">View</span>
          <div className="w-[180px]">
            <TabSwitch
              tabs={[
                { id: 'appearance', label: 'Appearance' },
                { id: 'counter', label: 'Counter' },
              ]}
              activeTab={activePanel}
              onTabChange={setActivePanel}
            />
          </div>
        </div>
      </section>
    </main>
  );
}

function DropdownFixture() {
  const [value, setValue] = useState('surface');
  return (
    <main data-dmn-app-chrome className="primitive-stage" data-state="dropdown">
      <section className="primitive-card">
        <h1 className="primitive-heading">Portal positioning</h1>
        <p className="primitive-label" style={{ width: 'auto' }}>
          The trigger is intentionally close to the lower-right viewport edge.
        </p>
        <output data-testid="dropdown-value">{value}</output>
      </section>
      <div className="primitive-dropdown-anchor">
        <Dropdown
          ariaLabel="Theme"
          value={value}
          onChange={setValue}
          align="right"
          widthClass="w-[170px]"
          options={[
            { value: 'app', label: 'Application' },
            { value: 'panel', label: 'Panel' },
            { value: 'surface', label: 'Surface' },
            { value: 'elevated', label: 'Elevated' },
          ]}
        />
      </div>
    </main>
  );
}

function ModalFixture() {
  const [open, setOpen] = useState(true);
  const anchorRef = useRef<HTMLButtonElement>(null);
  return (
    <main data-dmn-app-chrome className="primitive-stage" data-state="modal">
      <Button ref={anchorRef} onClick={() => setOpen(true)}>
        Open dialog
      </Button>
      <FloatingPopup
        open
        fixedX={1040}
        fixedY={92}
        role="menu"
        ariaLabel="Quick actions"
        className="primitive-popup-content"
      >
        Floating popup
      </FloatingPopup>
      <FloatingTooltip
        content="Keyboard accessible"
        delay={0}
        placement="bottom"
      >
        <Button className="ml-[12px]">Tooltip target</Button>
      </FloatingTooltip>
      {open && (
        <Modal onClick={() => setOpen(false)} ariaLabel="Primitive dialog">
          <section className="primitive-modal-card">
            <h1 className="primitive-heading">Edit appearance</h1>
            <input
              autoFocus
              aria-label="Dialog name"
              defaultValue="Overlay style"
              className="dmn-text-field h-[30px] px-[10px]"
            />
            <div className="flex justify-end gap-[8px]">
              <Button
                data-testid="dialog-cancel"
                onClick={() => setOpen(false)}
              >
                Cancel
              </Button>
              <Button variant="primary" data-testid="dialog-save">
                Save
              </Button>
            </div>
          </section>
        </Modal>
      )}
      <output data-testid="modal-state">{open ? 'open' : 'closed'}</output>
    </main>
  );
}

const root = document.getElementById('root');
if (!root) throw new Error('Primitive fixture root not found');

const fixture =
  state === 'dropdown' ? (
    <DropdownFixture />
  ) : state === 'modal' ? (
    <ModalFixture />
  ) : (
    <ControlsFixture />
  );

createRoot(root).render(fixture);
document.fonts.ready.then(() => {
  window.__VISUAL_READY__ = true;
});
