import Toggle from './Toggle';

interface CheckboxProps {
  checked: boolean;
  onChange: () => void;
}

const Checkbox = ({ checked, onChange }: CheckboxProps) => {
  const handleClick = (event: React.MouseEvent<HTMLButtonElement>) => {
    event.stopPropagation();
  };

  return <Toggle checked={checked} onChange={onChange} onClick={handleClick} />;
};

export default Checkbox;
