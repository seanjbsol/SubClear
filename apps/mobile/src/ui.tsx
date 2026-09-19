import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { colours, lightBackground, lightColour, lightLabel } from './theme';

export function TrafficLight({ light, size = 'regular' }: { light: string; size?: 'regular' | 'large' }) {
  const compact = size === 'regular';
  return (
    <View
      style={[
        styles.pill,
        {
          backgroundColor: lightBackground(light),
          paddingHorizontal: compact ? 8 : 12,
          paddingVertical: compact ? 4 : 8
        }
      ]}
    >
      <View style={[styles.dot, { backgroundColor: lightColour(light) }]} />
      <Text style={[styles.label, { color: lightColour(light), fontSize: compact ? 12 : 16 }]}>
        {lightLabel(light)}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  pill: {
    flexDirection: 'row',
    alignItems: 'center',
    alignSelf: 'flex-start',
    borderRadius: 999,
    gap: 6
  },
  dot: {
    width: 8,
    height: 8,
    borderRadius: 4
  },
  label: {
    fontWeight: '700',
    letterSpacing: 0.3
  }
});

export function FieldLabel({ children }: { children: React.ReactNode }) {
  return <Text style={fieldStyles.label}>{children}</Text>;
}

const fieldStyles = StyleSheet.create({
  label: {
    color: colours.muted,
    fontSize: 13,
    fontWeight: '600',
    marginBottom: 6
  }
});
