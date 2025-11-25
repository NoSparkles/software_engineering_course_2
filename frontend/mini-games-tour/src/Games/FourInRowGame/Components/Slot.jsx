import React from 'react';

export const Slot = ({ value, onClick, isClickable }) => {
  const getColor = () => {
    if (value === 'R') return '#ff4444'; // Red
    if (value === 'Y') return '#ffd700'; // Yellow/Gold
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
        boxShadow: value === 'R' ? '0 4px 12px rgba(255, 68, 68, 0.4)' : value === 'Y' ? '0 4px 12px rgba(255, 215, 0, 0.4)' : 'none',
        transition: 'all 0.2s ease',
      }}
    />
  );
};