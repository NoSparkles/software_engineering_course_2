import React from 'react';

export const Slot = ({ value, onClick, isClickable }) => {
  const getColor = () => {
    if (value === 'R') return 'red';
    if (value === 'Y') return 'yellow';
    return 'white';
  };

  return (
    <div
      onClick={isClickable ? onClick : undefined}
      style={{
        width: '48px',
        height: '48px',
        border: '2px solid #333',
        borderRadius: '50%',
        backgroundColor: getColor(),
        margin: '1px',
        cursor: isClickable ? 'pointer' : 'default',
        boxSizing: 'border-box',
      }}
    />
  );
};