import React from 'react';

export const Slot = ({ value, onClick, isClickable }) => {
  const getColor = () => {
    if (value === 'R') return '#7db8ff'; 
    if (value === 'Y') return '#a78bfa'; 
    return 'rgba(255, 255, 255, 0.1)';
  };

  return (
    <div
      onClick={isClickable ? onClick : undefined}
      style={{
        width: '48px',
        height: '48px',
        border: '2px solid rgba(255, 255, 255, 0.12)',
        borderRadius: '50%',
        backgroundColor: getColor(),
        margin: '1px',
        cursor: isClickable ? 'pointer' : 'default',
        boxSizing: 'border-box',
        boxShadow: value ? '0 4px 12px rgba(125, 184, 255, 0.3)' : 'none',
        transition: 'all 0.2s ease',
      }}
    />
  );
};