import React, { useState } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput
} from 'react-native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useAuth } from '../AuthContext';
import { colours } from '../theme';
import type { AuthStackParamList } from '../navigationTypes';

type Props = NativeStackScreenProps<AuthStackParamList, 'Register'>;

export function RegisterScreen({ navigation }: Props) {
  const { register } = useAuth();
  const [organisationName, setOrganisationName] = useState('');
  const [companyNumber, setCompanyNumber] = useState('');
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const onSubmit = async () => {
    setBusy(true);
    setError(null);
    try {
      await register({
        organisationName: organisationName.trim(),
        companyNumber: companyNumber.trim() || undefined,
        fullName: fullName.trim(),
        email: email.trim(),
        password
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Registration failed.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <KeyboardAvoidingView style={styles.flex} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
        <Text style={styles.title}>Create your organisation</Text>
        <Text style={styles.lede}>
          Registering creates a SubClear tenant for your main-contractor firm and makes you the Owner.
        </Text>

        <Text style={styles.label}>Organisation name</Text>
        <TextInput style={styles.input} value={organisationName} onChangeText={setOrganisationName} placeholder="Humber Civils Ltd" />
        <Text style={styles.label}>Companies House number (optional)</Text>
        <TextInput style={styles.input} value={companyNumber} onChangeText={setCompanyNumber} autoCapitalize="characters" />
        <Text style={styles.label}>Your name</Text>
        <TextInput style={styles.input} value={fullName} onChangeText={setFullName} />
        <Text style={styles.label}>Work email</Text>
        <TextInput
          style={styles.input}
          value={email}
          onChangeText={setEmail}
          autoCapitalize="none"
          keyboardType="email-address"
        />
        <Text style={styles.label}>Password (min. 10 characters)</Text>
        <TextInput style={styles.input} value={password} onChangeText={setPassword} secureTextEntry />

        {error ? <Text style={styles.error}>{error}</Text> : null}

        <Pressable style={[styles.button, busy && styles.disabled]} onPress={onSubmit} disabled={busy}>
          <Text style={styles.buttonText}>{busy ? 'Creating…' : 'Create tenant'}</Text>
        </Pressable>
        <Pressable onPress={() => navigation.goBack()}>
          <Text style={styles.link}>Back to sign in</Text>
        </Pressable>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1, backgroundColor: colours.paper },
  container: { padding: 24, paddingTop: 24 },
  title: { color: colours.navy, fontSize: 28, fontWeight: '800' },
  lede: { color: colours.muted, marginTop: 8, marginBottom: 24, lineHeight: 22 },
  label: { color: colours.muted, fontWeight: '600', marginBottom: 6 },
  input: {
    backgroundColor: colours.white,
    borderColor: colours.line,
    borderWidth: 1,
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 12,
    fontSize: 16,
    marginBottom: 14,
    color: colours.ink
  },
  button: { backgroundColor: colours.navy, borderRadius: 10, paddingVertical: 14, alignItems: 'center', marginTop: 8 },
  disabled: { opacity: 0.6 },
  buttonText: { color: colours.white, fontWeight: '700', fontSize: 16 },
  link: { color: colours.navyMid, textAlign: 'center', marginTop: 18, fontWeight: '600' },
  error: { color: colours.red, marginBottom: 8 }
});
